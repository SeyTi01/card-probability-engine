using System.IO.Compression;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Text.Json;
using System.Xml.Linq;

if (args.Length != 4) {
    throw new ArgumentException("Usage: PackageInspection <nupkg> <snupkg> <version> <source-commit>");
}

string packagePath = Path.GetFullPath(args[0]);
string symbolsPath = Path.GetFullPath(args[1]);
string version = args[2];
string commit = args[3];
const string repository = "https://github.com/SeyTi01/card-probability-engine";
using ZipArchive package = ZipFile.OpenRead(packagePath);
using ZipArchive symbols = ZipFile.OpenRead(symbolsPath);
XDocument specification = Xml(package, "CardProbability.Engine.nuspec");
XElement metadata = specification.Root!.Elements().Single(element => element.Name.LocalName == "metadata");
Require(Value(metadata, "id") == "CardProbability.Engine", "package ID");
Require(Value(metadata, "version") == version, "package version");
Require(Value(metadata, "authors") == "SeyTi01", "public author identity");
Require(Value(metadata, "projectUrl") == repository, "project URL");
Require(Value(metadata, "readme") == "README.md", "package README declaration");
XElement license = Element(metadata, "license");
Require(license.Value == "MIT" && (string?)license.Attribute("type") == "expression", "MIT license expression");
XElement source = Element(metadata, "repository");
Require((string?)source.Attribute("url") == repository && (string?)source.Attribute("type") == "git", "source repository");
Require((string?)source.Attribute("commit") == commit, "source commit");
XElement symbolMetadata = Xml(symbols, "CardProbability.Engine.nuspec").Root!.Elements().Single(element => element.Name.LocalName == "metadata");
Require(Value(symbolMetadata, "id") == "CardProbability.Engine" && Value(symbolMetadata, "version") == version, "symbol package identity");
XElement symbolSource = Element(symbolMetadata, "repository");
Require((string?)symbolSource.Attribute("url") == repository && (string?)symbolSource.Attribute("commit") == commit, "symbol package source provenance");
Require(!metadata.Descendants().Any(element => element.Name.LocalName == "dependency"), "no production package dependencies");
string[] assets = package.Entries.Where(entry => entry.FullName.StartsWith("lib/", StringComparison.Ordinal)).Select(entry => entry.FullName).Order().ToArray();
Require(assets.SequenceEqual(["lib/net10.0/CardProbability.dll", "lib/net10.0/CardProbability.xml"]), "only intended net10.0 DLL/XML assets");
Require(Text(package, "LICENSE").Contains("Copyright (c) 2025 SeyTi01", StringComparison.Ordinal), "original MIT attribution");
Require(Text(package, "README.md").Contains("CardProbability.Engine", StringComparison.Ordinal), "README content");
XDocument documentation = Xml(package, "lib/net10.0/CardProbability.xml");
Require(documentation.Root!.Element("assembly")!.Element("name")!.Value == "CardProbability", "IntelliSense assembly identity");
HashSet<string> documented = documentation.Descendants("member").Select(member => (string)member.Attribute("name")!).ToHashSet(StringComparer.Ordinal);
Require(documented.Contains("T:CardProbability.CalculationEngine") && documented.Contains("P:CardProbability.CalculationResult.ModelVersion"), "public API IntelliSense XML");

byte[] assemblyBytes = Bytes(package, "lib/net10.0/CardProbability.dll");
byte[] pdbBytes = Bytes(symbols, "lib/net10.0/CardProbability.pdb");
using MemoryStream assemblyStream = new(assemblyBytes, writable: false);
using PEReader assembly = new(assemblyStream);
MetadataReader assemblyMetadata = assembly.GetMetadataReader();
AssemblyDefinition definition = assemblyMetadata.GetAssemblyDefinition();
Require(assemblyMetadata.GetString(definition.Name) == "CardProbability", "assembly name");
CustomAttribute informationalVersion = definition.GetCustomAttributes().Select(assemblyMetadata.GetCustomAttribute).Single(attribute => AttributeName(assemblyMetadata, attribute) == "AssemblyInformationalVersionAttribute");
BlobReader versionReader = assemblyMetadata.GetBlobReader(informationalVersion.Value);
Require(versionReader.ReadUInt16() == 1 && versionReader.ReadSerializedString() == $"{version}+{commit}", "assembly version and source commit metadata");
DebugDirectoryEntry codeViewEntry = assembly.ReadDebugDirectory().Single(entry => entry.Type == DebugDirectoryEntryType.CodeView);
CodeViewDebugDirectoryData codeView = assembly.ReadCodeViewDebugDirectoryData(codeViewEntry);
using MemoryStream pdbStream = new(pdbBytes, writable: false);
using MetadataReaderProvider provider = MetadataReaderProvider.FromPortablePdbStream(pdbStream);
MetadataReader pdb = provider.GetMetadataReader();
Require(codeView.Guid == new Guid(pdb.DebugMetadataHeader!.Id.AsSpan(0, 16)), "symbol package PDB matches the packaged DLL");
Guid sourceLinkKind = new("CC110556-A091-4D38-9FEC-25AB9A351A6A");
CustomDebugInformation sourceLink = pdb.CustomDebugInformation.Select(pdb.GetCustomDebugInformation).Single(info => pdb.GetGuid(info.Kind) == sourceLinkKind);
using JsonDocument mapping = JsonDocument.Parse(pdb.GetBlobBytes(sourceLink.Value));
JsonProperty[] mappings = mapping.RootElement.GetProperty("documents").EnumerateObject().ToArray();
Require(mappings.Length > 0, "Source Link document mapping");
string expectedRemote = $"https://raw.githubusercontent.com/SeyTi01/card-probability-engine/{commit}/";
Require(mappings.All(item => item.Value.GetString()!.StartsWith(expectedRemote, StringComparison.Ordinal)), "commit-specific GitHub Source Link");
foreach (DocumentHandle handle in pdb.Documents) {
    string document = pdb.GetString(pdb.GetDocument(handle).Name);
    Require(document.StartsWith("/_/", StringComparison.Ordinal), "deterministic source paths without machine paths");
    Require(mappings.Any(item => Matches(item.Name, document)), "every PDB source document has Source Link mapping");
}
Console.WriteLine($"Package inspected: CardProbability.Engine {version}, net10.0, MIT, README/XML, BCL-only dependencies, matching portable PDB and Source Link at {commit}.");

static XElement Element(XElement metadata, string name) => metadata.Elements().Single(element => element.Name.LocalName == name);
static string Value(XElement metadata, string name) => Element(metadata, name).Value;
static byte[] Bytes(ZipArchive archive, string path) {
    ZipArchiveEntry entry = archive.GetEntry(path) ?? throw new InvalidOperationException($"Missing archive asset: {path}");
    using Stream input = entry.Open();
    using MemoryStream output = new();
    input.CopyTo(output);
    return output.ToArray();
}
static string Text(ZipArchive archive, string path) => System.Text.Encoding.UTF8.GetString(Bytes(archive, path));
static XDocument Xml(ZipArchive archive, string path) {
    using MemoryStream input = new(Bytes(archive, path), writable: false);
    return XDocument.Load(input);
}
static bool Matches(string pattern, string value) => pattern.EndsWith('*') ? value.StartsWith(pattern[..^1], StringComparison.Ordinal) : pattern == value;
static string AttributeName(MetadataReader reader, CustomAttribute attribute) {
    if (attribute.Constructor.Kind != HandleKind.MemberReference) {
        return "";
    }
    MemberReference constructor = reader.GetMemberReference((MemberReferenceHandle)attribute.Constructor);
    return constructor.Parent.Kind == HandleKind.TypeReference ? reader.GetString(reader.GetTypeReference((TypeReferenceHandle)constructor.Parent).Name) : "";
}
static void Require(bool condition, string description) {
    if (!condition) {
        throw new InvalidOperationException($"Package verification failed: {description}.");
    }
}
