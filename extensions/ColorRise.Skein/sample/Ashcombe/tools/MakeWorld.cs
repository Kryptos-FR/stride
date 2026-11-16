// Writes the world of the Ashcombe sample: materials, procedural models, the scenes of its places and the font. A .NET
// file-based app; from the folder of the sample:
//
//     dotnet run tools/MakeWorld.cs -- Ashcombe.Game
//
// Ids are stable (UUID v5 of a name), so a run over the same names writes the same files. The scenes are written whole:
// a change made to them in Game Studio is lost when this runs again, so change the world here.

using System.Globalization;
using System.Security.Cryptography;
using System.Text;

var game = args.Length > 0 ? args[0] : ".";
var world = new World(Path.Combine(game, "Assets"));
world.Build();
world.Write();

/// <summary>
/// A component of an entity: its YAML tag, its body (written from the scene, the entity and the ids of the scene) and,
/// for the components others point at, the name they use.
/// </summary>
internal sealed record Comp(string Kind, Func<string, Ent, IReadOnlyDictionary<string, string>, string> Body, string? Id = null);

/// <summary>
/// An entity of a scene: a name, a position, a model, components and children.
/// </summary>
internal sealed record Ent(string Name, (double X, double Y, double Z) Pos = default, string? Model = null, (double X, double Y, double Z, double W)? Rot = null, IReadOnlyList<Comp>? Comps = null, IReadOnlyList<Ent>? Children = null)
{
    public (double X, double Y, double Z, double W) Rotation => Rot ?? (0, 0, 0, 1);

    public IReadOnlyList<Comp> Components => Comps ?? [];

    public IReadOnlyList<Ent> Kids => Children ?? [];
}

internal sealed record Model(string Shape, string Material, (double, double, double)? Size = null, (double, double)? Plane = null, double Height = 0, double Radius = 0, double Length = 0, (double X, double Y, double Z) Offset = default);

internal sealed record SceneDef(string Url, IReadOnlyList<Ent> Entities, (double X, double Y, double Z) Offset, IReadOnlyList<string> Children);

internal sealed class World(string assets)
{
    private static readonly Guid Namespace = Guid.Parse("6f1d2c3e-6a0b-4b3e-9d1a-a5c0b1e7d401");

    private const string Slot = "d0a6bf72-b3cd-4bd4-94ca-69952999d537"; // the camera slot of the compositor copied from Brinemoor

    private static readonly string Font = $"{Uid("font")}:UI/Font";
    private static readonly string Story = $"{Uid("yarn", "Ashcombe")}:Dialogue/Ashcombe";
    private static readonly string Echo = $"{Uid("yarn", "Echo")}:Dialogue/Echo/Echo";

    private static readonly (double, double, double, double) SunRotation = (-0.450803, -0.191984, -0.099941, 0.865985);

    private static readonly (string Name, (double R, double G, double B) Color)[] Materials =
    [
        ("Grass", (0.33, 0.5, 0.26)), ("Path", (0.62, 0.54, 0.4)), ("Water", (0.2, 0.38, 0.56)), ("Stone", (0.56, 0.56, 0.55)),
        ("Wood", (0.45, 0.3, 0.18)), ("Plaster", (0.86, 0.8, 0.68)), ("Roof", (0.5, 0.22, 0.16)), ("Dark", (0.12, 0.12, 0.14)),
        ("Rock", (0.36, 0.33, 0.3)), ("Floor", (0.42, 0.34, 0.25)), ("CastleStone", (0.62, 0.6, 0.54)), ("Gold", (0.86, 0.7, 0.3)),
        ("Banner", (0.52, 0.12, 0.16)), ("Herb", (0.35, 0.68, 0.3)), ("Paper", (0.93, 0.9, 0.82)), ("Cloth", (0.3, 0.45, 0.62)),
        ("Cartographer", (0.2, 0.45, 0.78)), ("Hedda", (0.78, 0.36, 0.25)), ("Bram", (0.72, 0.66, 0.55)), ("Lio", (0.96, 0.76, 0.3)),
        ("Nan", (0.45, 0.62, 0.36)), ("Maren", (0.24, 0.3, 0.52)), ("Pell", (0.62, 0.4, 0.68)), ("Ysolde", (0.58, 0.15, 0.36)),
        ("Corvin", (0.16, 0.16, 0.22)), ("Ottilie", (0.48, 0.42, 0.3)), ("Agathe", (0.92, 0.9, 0.86)), ("Gudrun", (0.52, 0.3, 0.15)),
        ("Jory", (0.42, 0.36, 0.3)), ("Anselm", (0.62, 0.56, 0.42)), ("Townsfolk", (0.55, 0.48, 0.42)), ("Miner", (0.4, 0.38, 0.36)),
    ];

    private static readonly (string Name, (double R, double G, double B) Color, double Intensity)[] Emissive =
    [
        ("Glow", (0.45, 0.95, 0.85), 5.0), ("Fire", (1.0, 0.6, 0.25), 4.0), ("Lamp", (1.0, 0.85, 0.45), 4.0),
    ];

    private readonly List<(string Name, Model Model)> models = [];
    private readonly List<SceneDef> scenes = [];

    // ---------------------------------------------------------------- ids and numbers

    /// <summary>
    /// The UUID v5 of a name, as Python's <c>uuid.uuid5</c> gives it.
    /// </summary>
    private static string Uid(params string[] parts) => Uuid5(string.Join("/", parts)).ToString();

    /// <summary>
    /// The key of an item of a collection: a UUID v5 without dashes.
    /// </summary>
    private static string Key(params string[] parts) => Uuid5("key/" + string.Join("/", parts)).ToString("N");

    private static Guid Uuid5(string name)
    {
        var ns = Namespace.ToByteArray(true);
        var hash = SHA1.HashData([.. ns, .. Encoding.UTF8.GetBytes(name)]);
        var bytes = hash[..16];
        bytes[6] = (byte)((bytes[6] & 0x0F) | 0x50);
        bytes[8] = (byte)((bytes[8] & 0x3F) | 0x80);
        return new Guid(bytes, true);
    }

    /// <summary>
    /// A number as the scene files have it: the shortest text that reads back the same, with ".0" when it is whole.
    /// </summary>
    private static string F(double value)
    {
        var text = value.ToString("R", CultureInfo.InvariantCulture);
        return text.Contains('.') || text.Contains('E') ? text : text + ".0";
    }

    private static string V3((double X, double Y, double Z) v) => $"{{X: {F(v.X)}, Y: {F(v.Y)}, Z: {F(v.Z)}}}";

    private static string Rgb((double R, double G, double B) c) => $"R: {F(c.R)}, G: {F(c.G)}, B: {F(c.B)}";

    private static string Bool(bool value) => value ? "true" : "false";

    private static string OrEmpty(string value) => value.Length > 0 ? value : "''";

    private static (double, double, double, double) QuatZ(double degrees)
    {
        var a = degrees * Math.PI / 180 / 2;
        return (0, 0, Math.Round(Math.Sin(a), 6), Math.Round(Math.Cos(a), 6));
    }

    private static (double, double, double, double) QuatY(double degrees)
    {
        var a = degrees * Math.PI / 180 / 2;
        return (0, Math.Round(Math.Sin(a), 6), 0, Math.Round(Math.Cos(a), 6));
    }

    private void WriteFile(string path, string text)
    {
        var full = Path.Combine(assets, path);
        Directory.CreateDirectory(Path.GetDirectoryName(full)!);
        File.WriteAllText(full, text, new UTF8Encoding(false));
    }

    // ---------------------------------------------------------------- materials

    private string MaterialText(string name, (double R, double G, double B) color, double? emissive)
    {
        var text = $$"""
            !MaterialAsset
            Id: {{Uid("material", name)}}
            SerializedVersion: {Stride: 2.0.0.0}
            Tags: []
            Attributes:
                MicroSurface: !MaterialGlossinessMapFeature
                    GlossinessMap: !ComputeFloat
                        Value: 0.25
                Diffuse: !MaterialDiffuseMapFeature
                    DiffuseMap: !ComputeColor
                        Value: {{{Rgb(color)}}, A: 1.0}
                DiffuseModel: !MaterialDiffuseLambertModelFeature {}
                Specular: !MaterialMetalnessMapFeature
                    MetalnessMap: !ComputeFloat
                        Value: 0.0
                SpecularModel: !MaterialSpecularMicrofacetModelFeature
                    Fresnel: !MaterialSpecularMicrofacetFresnelSchlick {}
                    Visibility: !MaterialSpecularMicrofacetVisibilitySmithSchlickGGX {}
                    NormalDistribution: !MaterialSpecularMicrofacetNormalDistributionGGX {}

            """;
        if (emissive is { } intensity)
        {
            text += $$"""
                    Emissive: !MaterialEmissiveMapFeature
                        EmissiveMap: !ComputeColor
                            Value: {{{Rgb(color)}}, A: 1.0}
                        Intensity: !ComputeFloat
                            Value: {{F(intensity)}}

                """;
        }

        text += """
                Overrides:
                    UVScale: {X: 1.0, Y: 1.0}
            Layers: {}

            """;
        return text;
    }

    private static string MaterialRef(string name) => $"{Uid("material", name)}:World/Materials/{name}";

    // ---------------------------------------------------------------- models

    private string AddModel(string name, Model model)
    {
        var index = models.FindIndex(m => m.Name == name);
        if (index >= 0)
            models[index] = (name, model);
        else
            models.Add((name, model));
        return name;
    }

    private string Box(string name, string material, double x, double y, double z)
        => AddModel(name, new Model("box", material, Size: (x, y, z), Offset: (0, y / 2, 0)));

    private string PlaneModel(string name, string material, double x, double z) => AddModel(name, new Model("plane", material, Plane: (x, z)));

    private string Capsule(string name, string material, double length, double radius, (double, double, double) offset)
        => AddModel(name, new Model("capsule", material, Length: length, Radius: radius, Offset: offset));

    private string Sphere(string name, string material, double radius, (double, double, double) offset = default)
        => AddModel(name, new Model("sphere", material, Radius: radius, Offset: offset));

    private string Cylinder(string name, string material, double height, double radius)
        => AddModel(name, new Model("cylinder", material, Height: height, Radius: radius));

    private string Person(string name) => Capsule(name, name, 0.9, 0.35, (0, 0.8, 0));

    private static string ModelText(string name, Model model)
    {
        var head = $"!ProceduralModelAsset\nId: {Uid("model", name)}\nSerializedVersion: {{Stride: 2.0.0.0}}\nTags: []\n";
        var body = model.Shape switch
        {
            "plane" => $"Type: !PlaneProceduralModel\n    Size: {{X: {F(model.Plane!.Value.Item1)}, Y: {F(model.Plane!.Value.Item2)}}}\n    Tessellation: {{X: 1, Y: 1}}\n",
            "box" => $"Type: !CubeProceduralModel\n    Size: {V3(model.Size!.Value)}\n",
            "cylinder" => $"Type: !CylinderProceduralModel\n    Height: {F(model.Height)}\n    Radius: {F(model.Radius)}\n    Tessellation: 24\n",
            "sphere" => $"Type: !SphereProceduralModel\n    Radius: {F(model.Radius)}\n    Tessellation: 16\n",
            "capsule" => $"Type: !CapsuleProceduralModel\n    Length: {F(model.Length)}\n    Radius: {F(model.Radius)}\n    Tessellation: 16\n",
            _ => throw new ArgumentException(model.Shape),
        };
        var tail = $"    Scale: {{X: 1.0, Y: 1.0, Z: 1.0}}\n    UvScale: {{X: 1.0, Y: 1.0}}\n    LocalOffset: {V3(model.Offset)}\n    NumberOfTextureCoordinates: 10\n    MaterialInstance:\n        Material: {MaterialRef(model.Material)}\n";
        return head + body + tail;
    }

    // ---------------------------------------------------------------- components

    private static Comp Talkable(string node, bool thing = false, string label = "", string? runner = null)
        => new("!Ashcombe.Talkable,Ashcombe.Game", (_, _, ids) => $"Node: {node}\nIsThing: {Bool(thing)}\nLabel: {OrEmpty(label)}\nWalkSpeed: 3.0\nRunner: {(runner is null ? "null" : $"ref!! {ids[runner]}")}");

    private static Comp Actor(string name) => new("!DialogueActorComponent", (_, _, _) => $"ActorName: {name}");

    private static Comp Shown(string variable, bool not = false)
        => new("!Ashcombe.ShownWhen,Ashcombe.Game", (_, _, _) => $"Variable: {variable}\nNot: {Bool(not)}");

    private static Comp Door(string target, string spawn = "", bool journey = false, string label = "", string node = "")
        => new("!Ashcombe.Doorway,Ashcombe.Game", (_, _, _) => $"Target: {Uid("scene", target)}:{target}\nSpawn: {OrEmpty(spawn)}\nIsJourney: {Bool(journey)}\nLabel: {OrEmpty(label)}\nNode: {OrEmpty(node)}");

    private static Comp StoryPlace(string place, string variable)
        => new("!Ashcombe.StoryPlace,Ashcombe.Game", (_, _, _) => $"Place: {Uid("scene", place)}:{place}\nVariable: {variable}");

    private static Comp Area(double hx, double hz, string title)
        => new("!Ashcombe.PlaceArea,Ashcombe.Game", (_, _, _) => $"HalfSize: {{X: {F(hx)}, Y: {F(hz)}}}\nTitle: {title}");

    private static Comp PointLight((double, double, double) color, double radius, double intensity)
        => new("!LightComponent", (_, _, _) => $"Type: !LightPoint\n    Color: !ColorRgbProvider\n        Value: {{{Rgb(color)}}}\n    Radius: {F(radius)}\nIntensity: {F(intensity)}");

    private static Comp Sun(double intensity = 4.0, (double, double, double)? color = null)
        => new("!LightComponent", (_, _, _) => $$"""
            Type: !LightDirectional
                Color: !ColorRgbProvider
                    Value: {{{Rgb(color ?? (1.0, 0.97, 0.9))}}}
                Shadow:
                    Enabled: true
                    Filter: !LightShadowMapFilterTypePcf
                        FilterSize: Filter5x5
                    Size: Large
                    DepthRange: {}
                    PartitionMode: !LightDirectionalShadowMap.PartitionLogarithmic {}
                    ComputeTransmittance: false
                    BiasParameters: {}
            Intensity: {{F(intensity)}}
            """);

    private static Comp Ambient(double intensity, (double, double, double)? color = null)
        => new("!LightComponent", (_, _, _) => $"Type: !LightAmbient\n    Color: !ColorRgbProvider\n        Value: {{{Rgb(color ?? (0.8, 0.85, 1.0))}}}\nIntensity: {F(intensity)}");

    private static Comp Runner(string name, string project, params string[] presenters)
        => new("!DialogueRunnerComponent", (scene, ent, ids) =>
            $"Project: {project}\nStartNode: ''\nAutoStart: false\nPresenters:\n{string.Join("\n", presenters.Select(p => $"    {Key(scene, ent.Name, "presenter", p)}: ref!! {ids[p]}"))}\nSaliency: null\nOptionsFallThrough: true", name);

    private static Comp LinePresenter(string name)
        => new("!SkeinLinePresenter", (_, _, _) => $"Page: null\nFont: {Font}\nTypewriter: Letter\nSpeed: 45.0\nAutoAdvance: false\nHoldTime: 1.5", name);

    private static Comp OptionsPresenter(string name)
        => new("!SkeinOptionsPresenter", (_, _, _) => $"Page: null\nFont: {Font}\nUnavailable: Hidden\nPreviousKey: Up\nNextKey: Down\nChooseKey: Enter\nChooseButton: A\nNumberKeys: true", name);

    private static Comp Advancer(string name)
        => new("!SkeinLineAdvancer", (_, _, _) => "AdvanceKey: Space\nAdvanceButton: A\nAdvanceWithPointer: true", name);

    private static Comp Mood(string name) => new("!Ashcombe.MoodPresenter,Ashcombe.Game", (_, _, _) => "", name);

    private static Comp Caption(string name)
        => new("!Ashcombe.CaptionPresenter,Ashcombe.Game", (_, _, _) => $"Font: {Font}\nHoldTime: 1.6\nTimePerLetter: 0.045", name);

    private static Comp Chatter(string runner, string node, double distance = 7.0)
        => new("!Ashcombe.Chatter,Ashcombe.Game", (_, _, ids) => $"Runner: ref!! {ids[runner]}\nNode: {node}\nListener: null\nDistance: {F(distance)}\nPause: 9.0");

    private static Comp Camera() => new("!CameraComponent", (_, _, _) => $"Name: null\nProjection: Perspective\nSlot: {Slot}");

    private static Comp PlayerController()
        => new("!Ashcombe.PlayerController,Ashcombe.Game", (_, _, ids) => $"Speed: 5.0\nCamera: ref!! {ids["entity:Camera"]}");

    private static Comp Stage(params string[] projects)
        => new("!Ashcombe.Stage,Ashcombe.Game", (scene, _, ids) =>
        {
            var text = $"Runner: ref!! {ids["StoryRunner"]}\nPlayer: ref!! {ids["entity:Cartographer"]}\nCamera: ref!! {ids["entity:Camera"]}\nFont: {Font}\nReach: 2.5";
            return text + (projects.Length > 0
                ? "\nProjects:\n" + string.Join("\n", projects.Select(p => $"    {Key(scene, "projects", p)}: {p}"))
                : "\nProjects: {}");
        });

    /// <summary>
    /// The parts every root scene has: the stage with its runner and presenters, the cartographer and the camera.
    /// </summary>
    private static Ent[] StageEntities((double X, double Y, double Z) start, bool lantern = false, params string[] projects)
    {
        Ent[] cartographerChildren = lantern ? [new Ent("Lantern", (0, 2.2, 0), Comps: [PointLight((1.0, 0.82, 0.5), 9, 3.0)])] : [];
        return
        [
            new Ent("Stage", Comps:
            [
                Runner("StoryRunner", Story, "Line", "Options", "Advancer", "Mood"),
                LinePresenter("Line"), OptionsPresenter("Options"), Advancer("Advancer"), Mood("Mood"),
                Stage(projects),
            ]),
            new Ent("Cartographer", start, "Cartographer", Comps: [PlayerController()], Children: cartographerChildren),
            new Ent("Camera", (start.X, start.Y + 13, start.Z + 12), Rot: (-0.401073, 0.0, 0.0, 0.916046), Comps: [Camera()]),
        ];
    }

    private static Ent PersonEntity(string name, (double, double, double) pos, string? node = null, Comp[]? comps = null, (double, double, double, double)? rot = null)
        => new(name, pos, name, rot, [Talkable(node ?? name), Actor(name), .. comps ?? []]);

    private static Ent Thing(string name, (double, double, double) pos, string model, string label, string? node = null, Comp[]? comps = null)
        => new(name, pos, model, Comps: [Talkable(node ?? name, thing: true, label: label), .. comps ?? []]);

    private static Ent Spawn(string name, (double, double, double) pos) => new(name, pos);

    private static Ent E(string name, (double, double, double) pos, string? model = null, params Comp[] comps) => new(name, pos, model, Comps: comps);

    // ---------------------------------------------------------------- scenes

    private void Scene(string url, IEnumerable<Ent> entities, (double, double, double) offset = default, params string[] children)
        => scenes.Add(new SceneDef(url, [.. entities], offset, children));

    private static string SceneText(SceneDef scene, string? parent)
    {
        var url = scene.Url;
        var ids = new Dictionary<string, string>();

        void Collect(Ent e)
        {
            ids[$"entity:{e.Name}"] = Uid("scene", url, "entity", e.Name);
            for (var n = 0; n < e.Components.Count; n++)
            {
                var c = e.Components[n];
                if (c.Id is not null)
                    ids[c.Id] = Uid("scene", url, e.Name, "comp", n.ToString(CultureInfo.InvariantCulture), c.Kind);
            }

            foreach (var child in e.Kids)
            {
                Collect(child);
            }
        }

        foreach (var e in scene.Entities)
        {
            Collect(e);
        }

        var parts = new List<string>();

        void Part(Ent e)
        {
            var comps = new List<string>();
            var childRefs = e.Kids.Count == 0 ? "{}" : "\n" + string.Join("\n", e.Kids.Select(c => $"                            {Key(url, e.Name, "child", c.Name)}: ref!! {Uid("scene", url, c.Name, "transform")}"));
            var r = e.Rotation;
            comps.Add($$"""
                                    {{Key(url, e.Name, "transform")}}: !TransformComponent
                                        Id: {{Uid("scene", url, e.Name, "transform")}}
                                        Position: {{V3(e.Pos)}}
                                        Rotation: {X: {{F(r.X)}}, Y: {{F(r.Y)}}, Z: {{F(r.Z)}}, W: {{F(r.W)}}}
                                        Scale: {X: 1.0, Y: 1.0, Z: 1.0}
                                        Children: {{childRefs}}
                """);
            if (e.Model is not null)
            {
                comps.Add($$"""
                                        {{Key(url, e.Name, "model")}}: !ModelComponent
                                            Id: {{Uid("scene", url, e.Name, "model")}}
                                            Model: {{Uid("model", e.Model)}}:World/Models/{{e.Model}}
                                            Materials: {}
                    """);
            }

            for (var n = 0; n < e.Components.Count; n++)
            {
                var c = e.Components[n];
                var index = n.ToString(CultureInfo.InvariantCulture);
                var body = c.Body(url, e, ids);
                var text = $"                    {Key(url, e.Name, "comp", index)}: {c.Kind}\n                        Id: {Uid("scene", url, e.Name, "comp", index, c.Kind)}";
                if (body.Length > 0)
                    text += "\n" + string.Join("\n", body.Split('\n').Select(line => "                        " + line));
                comps.Add(text);
            }

            parts.Add($"        -   Entity:\n                Id: {ids[$"entity:{e.Name}"]}\n                Name: {e.Name}\n                Components:\n" + string.Join("\n", comps));
            foreach (var child in e.Kids)
            {
                Part(child);
            }
        }

        foreach (var e in scene.Entities)
        {
            Part(e);
        }

        var parentText = parent is null ? "" : $"Parent: {Uid("scene", parent)}:{parent}\n";
        var childrenText = scene.Children.Count == 0 ? "[]" : "\n" + string.Join("\n", scene.Children.Select(c => $"    - {Uid("scene", c)}"));
        var roots = string.Join("\n", scene.Entities.Select(e => $"        - ref!! {ids[$"entity:{e.Name}"]}"));
        return $"!SceneAsset\nId: {Uid("scene", url)}\nSerializedVersion: {{Stride: 3.1.0.1}}\nTags: []\nChildrenIds: {childrenText}\n{parentText}Offset: {V3(scene.Offset)}\nHierarchy:\n    RootParts:\n{roots}\n    Parts:\n"
            + string.Join("\n", parts) + "\n";
    }

    /// <summary>
    /// A floor and low walls around it, open in the middle of the front (+Z) wall for the door.
    /// </summary>
    private Ent[] Room(string prefix, double w, double d, string floor = "Floor", string wall = "Plaster", double gap = 2.4)
    {
        PlaneModel($"{prefix}Floor", floor, w, d);
        Box($"{prefix}WallBack", wall, w, 1.4, 0.3);
        Box($"{prefix}WallSide", wall, 0.3, 1.4, d);
        var side = (w - gap) / 2;
        Box($"{prefix}WallFront", wall, side, 1.4, 0.3);
        return
        [
            new Ent("Floor", Model: $"{prefix}Floor"),
            E("WallBack", (0, 0, -d / 2), $"{prefix}WallBack"),
            E("WallLeft", (-w / 2, 0, 0), $"{prefix}WallSide"),
            E("WallRight", (w / 2, 0, 0), $"{prefix}WallSide"),
            E("WallFrontLeft", (-(gap / 2 + side / 2), 0, d / 2), $"{prefix}WallFront"),
            E("WallFrontRight", (gap / 2 + side / 2, 0, d / 2), $"{prefix}WallFront"),
        ];
    }

    private static Ent IndoorLight((double, double, double)? pos = null, double radius = 14, double intensity = 6, (double, double, double)? color = null)
        => E("Lamp", pos ?? (0, 4, 0), null, PointLight(color ?? (1.0, 0.86, 0.62), radius, intensity));

    // ================================================================ the world

    public void Build()
    {
        foreach (var p in new[] { "Cartographer", "Hedda", "Bram", "Lio", "Nan", "Maren", "Pell", "Ysolde", "Corvin", "Ottilie", "Agathe", "Gudrun", "Jory", "Anselm" })
        {
            Person(p);
        }

        foreach (var p in new[] { "Wim", "Saul", "Miner1", "Miner2", "Fairgoer1", "Fairgoer2", "Fairgoer3" })
        {
            Capsule(p, p.StartsWith("Miner", StringComparison.Ordinal) ? "Miner" : "Townsfolk", 0.85, 0.33, (0, 0.78, 0));
        }

        // Shared shapes
        Box("Doorway", "Wood", 1.2, 2.2, 0.2);
        Box("Signpost", "Wood", 0.3, 1.8, 0.3);
        Box("Stone", "Stone", 0.5, 0.8, 0.5);
        Sphere("Herb", "Herb", 0.28, (0, 0.28, 0));
        Box("Crate", "Wood", 0.9, 0.9, 0.9);
        Box("Table", "Wood", 2.0, 0.9, 1.2);
        Box("Shelf", "Wood", 3.0, 2.2, 0.5);
        Box("Paper", "Paper", 0.8, 0.9, 0.6);
        Box("Wall", "Plaster", 1.0, 1.4, 1.0);

        BuildVillage();
        BuildInteriors();
        BuildCastle();
        BuildMine();
    }

    private void BuildVillage()
    {
        PlaneModel("VillageGround", "Grass", 56, 44);
        PlaneModel("VillagePath", "Path", 5, 44);
        PlaneModel("River", "Water", 56, 4);
        Box("Bridge", "Wood", 4, 0.3, 6);
        Box("InnHouse", "Plaster", 7, 4, 6);
        Box("InnRoof", "Roof", 7.4, 0.6, 6.4);
        Box("MillHouse", "Plaster", 7, 5, 6);
        Cylinder("MillWheel", "Wood", 0.5, 2.2);
        Box("CottageHouse", "Plaster", 5, 3, 4);
        Box("ChapelHall", "Stone", 6, 4, 7);
        Box("ChapelTower", "Stone", 2.2, 9, 2.2);
        Box("Lockup", "Stone", 3, 2.6, 3);
        Box("Cart", "Wood", 2.0, 1.2, 1.2);
        Box("Stones", "Stone", 1.8, 0.8, 0.5);
        Box("BridgeMark", "Paper", 0.4, 1.2, 0.4);

        Scene("Places/Village",
        [
            .. StageEntities((0, 0, 14), projects: Echo),
            new Ent("Sun", (0, 5, 0), Rot: SunRotation, Comps: [Sun()]),
            E("Ambient", (0, 5, 0), null, Ambient(0.8)),
            new Ent("Area", Comps: [Area(25, 19, "line:place_village")]),
            Spawn("Arrival", (0, 0, 14)), Spawn("InnFront", (-12, 0, 1.6)), Spawn("MillFront", (16, 0, 9.4)),
            Spawn("CottageFront", (-18, 0, 12.4)), Spawn("ChapelFront", (2, 0, -2.6)), Spawn("FromCastle", (12, 0, -12.5)),
            Spawn("FromMine", (-21.5, 0, 12)),
            new Ent("Ground", Model: "VillageGround"), E("Path", (0, 0.01, 0), "VillagePath"), E("River", (0, 0.02, -16), "River"),
            E("Bridge", (12, 0.0, -16), "Bridge"),
            E("InnHouse", (-12, 0, -4), "InnHouse"), E("InnRoof", (-12, 4, -4), "InnRoof"),
            E("MillHouse", (16, 0, 4), "MillHouse"), new Ent("MillWheel", (20.1, 2.4, 4), "MillWheel", QuatZ(90)),
            E("CottageHouse", (-18, 0, 8), "CottageHouse"),
            E("ChapelHall", (2, 0, -9), "ChapelHall"), E("ChapelTower", (2, 0, -13.6), "ChapelTower"),
            E("Lockup", (8, 0, -6), "Lockup"), E("PellsCart", (7, 0, -11.5), "Cart"),
            E("InnDoor", (-12, 0, -0.9), "Doorway", Door("Places/Village/Inn", label: "line:go_inn")),
            E("MillDoor", (16, 0, 7.1), "Doorway", Door("Places/Village/Mill", label: "line:go_mill")),
            E("CottageDoor", (-18, 0, 10.1), "Doorway", Door("Places/Village/Cottage", label: "line:go_cottage")),
            E("ChapelDoor", (2, 0, -5.4), "Doorway", Door("Places/Village/Chapel", label: "line:go_chapel")),
            E("CastleRoad", (12, 0, -19.5), "Signpost", Door("Places/Castle", "Gate", true, "line:go_castle", "CastleRoad")),
            E("MineRoad", (-25, 0, 12), "Signpost", Door("Places/Mine", "Camp", true, "line:go_mine", "MineRoad")),
            PersonEntity("Maren", (-3, 0, 4)),
            PersonEntity("Pell", (8.5, 0, -10), comps: [Shown("$pell_in_village")]),
            PersonEntity("Bram", (18.5, 0, 8.6)),
            PersonEntity("Corvin", (4, 0, 6), comps: [Shown("$corvin_in_village")]),
            Thing("Stones", (16, 0, -11), "Stones", "line:use_stones"),
            Thing("BridgeMark", (12, 0, -12.8), "BridgeMark", "line:use_survey"),
            Thing("DewCup", (-6, 0, -13.2), "Herb", "line:use_pick", comps: [Shown("$dewcup_taken", true)]),
            Thing("Nettle", (5.2, 0, -4.6), "Herb", "line:use_pick", comps: [Shown("$nettle_taken", true)]),
            Thing("Wheel", (20.6, 0, 6.8), "Crate", "line:use_wheel"),
            new Ent("FairGround", Comps: [StoryPlace("Places/Village/Fair", "$fair_up")]),
        ], default, "Places/Village/Inn", "Places/Village/Mill", "Places/Village/Cottage", "Places/Village/Chapel", "Places/Village/Fair");
    }

    private void BuildInteriors()
    {
        Box("Bar", "Wood", 4, 1.1, 1);
        Box("Barrel", "Wood", 0.8, 1.0, 0.8);
        Box("Well", "Stone", 1.6, 0.9, 1.6);
        Box("RecipeBox", "Gold", 0.6, 0.4, 0.4);
        Box("Millstones", "Stone", 2.4, 0.8, 2.4);
        Box("Sacks", "Paper", 1.4, 0.8, 1.0);
        Box("Ladder", "Wood", 0.8, 3.0, 0.2);
        Box("Altar", "Stone", 2.0, 1.1, 1.0);
        Box("BellRope", "Cloth", 0.15, 3.0, 0.15);
        Box("Pews", "Wood", 4.0, 0.7, 0.8);
        Box("MasonMark", "Stone", 0.8, 1.6, 0.3);
        Box("HerbTable", "Wood", 2.2, 0.9, 1.0);

        Scene("Places/Village/Inn",
        [
            new Ent("Area", Comps: [Area(6.5, 4.5, "line:place_inn")]), .. Room("Inn", 14, 10), IndoorLight(),
            Spawn("Arrival", (0, 0, 3.6)), Spawn("FromCellar", (4.2, 0, -2.6)),
            E("Bar", (-2, 0, -3.6), "Bar"), E("Barrel", (-5.5, 0, -4), "Barrel"),
            E("Table", (2.5, 0, 1.2), "Table"),
            PersonEntity("Hedda", (-2, 0, -2.4)),
            E("Wim", (1.4, 0, 2.2), "Wim"), E("Saul", (3.6, 0, 2.2), "Saul"),
            E("Regulars", (2.5, 0, 1.2), null, Runner("ChatRunner", Story, "Caption"), Caption("Caption"), Chatter("ChatRunner", "InnChatter")),
            E("InnExit", (0, 0, 4.9), "Doorway", Door("Places/Village", "InnFront", label: "line:go_out")),
            E("CellarStairs", (4.6, 0, -4.4), "Doorway", Door("Places/Village/Cellar", label: "line:go_cellar", node: "CellarStairs")),
        ], (200, 0, 0), "Places/Village/Cellar");

        Scene("Places/Village/Cellar",
        [
            new Ent("Area", Comps: [Area(4.5, 3.5, "line:place_cellar")]), .. Room("Cellar", 10, 8, "Floor", "Stone"),
            IndoorLight((0, 3, 0), 10, 3, (0.9, 0.75, 0.5)),
            Spawn("Arrival", (3, 0, -2)),
            Thing("Well", (-2, 0, 0), "Well", "line:use_survey"),
            Thing("RecipeBox", (2.5, 0, 2.2), "RecipeBox", "line:use_look", comps: [Shown("$recipe_found", true)]),
            Thing("Hollyroot", (-3.4, 0, 2.6), "Herb", "line:use_pick", comps: [Shown("$hollyroot_taken", true)]),
            E("Barrels", (3.6, 0, 2.6), "Barrel"),
            E("CellarUp", (3.6, 0, -3.4), "Doorway", Door("Places/Village/Inn", "FromCellar", label: "line:go_up")),
        ], (0, 0, -60));

        Scene("Places/Village/Mill",
        [
            new Ent("Area", Comps: [Area(5.5, 4.5, "line:place_mill")]), .. Room("Mill", 12, 10), IndoorLight(),
            Spawn("Arrival", (0, 0, 3.6)),
            E("Millstones", (1.5, 0, -1), "Millstones"),
            PersonEntity("Lio", (-2.5, 0, 0.5)),
            Thing("Loft", (-4.2, 0, -4.4), "Ladder", "line:use_survey"),
            Thing("Sacks", (3.8, 0, -3.6), "Sacks", "line:use_look"),
            E("MillExit", (0, 0, 4.9), "Doorway", Door("Places/Village", "MillFront", label: "line:go_out")),
        ], (300, 0, 0));

        Scene("Places/Village/Cottage",
        [
            new Ent("Area", Comps: [Area(4.5, 3.5, "line:place_cottage")]), .. Room("Cottage", 10, 8), IndoorLight((0, 3.5, 0), 11, 5, (0.95, 0.9, 0.7)),
            Spawn("Arrival", (0, 0, 2.6)),
            PersonEntity("Nan", (0.5, 0, -1.6)),
            Thing("HerbTable", (-2.5, 0, -2.6), "HerbTable", "line:use_look"),
            E("CottageExit", (0, 0, 3.9), "Doorway", Door("Places/Village", "CottageFront", label: "line:go_out")),
        ], (400, 0, 0));

        Scene("Places/Village/Chapel",
        [
            new Ent("Area", Comps: [Area(4.5, 5.5, "line:place_chapel")]), .. Room("Chapel", 10, 12, "Floor", "Stone"),
            IndoorLight((0, 4, -2), 14, 5, (0.85, 0.85, 1.0)),
            Spawn("Arrival", (0, 0, 4.6)),
            E("Altar", (0, 0, -4.6), "Altar"), E("Pews1", (0, 0, -1), "Pews"), E("Pews2", (0, 0, 1.2), "Pews"),
            Thing("Bell", (-3.6, 0, -5), "BellRope", "line:use_survey"),
            Thing("MasonMark", (3.8, 0, -3), "MasonMark", "line:use_look"),
            E("ChapelExit", (0, 0, 5.9), "Doorway", Door("Places/Village", "ChapelFront", label: "line:go_out")),
        ], (500, 0, 0));

        Box("Stall", "Banner", 2.6, 2.2, 1.4);
        Box("StallBlue", "Cloth", 2.6, 2.2, 1.4);
        Box("HearingTable", "Wood", 4.0, 0.9, 1.4);
        Box("Flag", "Banner", 0.2, 3.5, 0.2);

        Scene("Places/Village/Fair",
        [
            E("StallHedda", (-9, 0, 7), "Stall"), E("StallBram", (-5, 0, 7.5), "StallBlue"), E("StallCheese", (7.5, 0, 7.5), "Stall"),
            E("Flag1", (-2.5, 0, 9), "Flag"), E("Flag2", (2.5, 0, 9), "Flag"),
            Thing("HearingTable", (0, 0, 9.5), "HearingTable", "line:use_hearing", node: "Hearing"),
            PersonEntity("Ysolde", (-0.8, 0, 8.2), node: "Ysolde", rot: QuatY(180)),
            PersonEntity("Corvin", (1.6, 0, 8.2), node: "Corvin", rot: QuatY(180), comps: [Shown("$corvin_at_fair")]),
            PersonEntity("Anselm", (3.4, 0, 10.4)),
            E("Fairgoer1", (-7, 0, 9.5), "Fairgoer1"), E("Fairgoer2", (-3.5, 0, 11), "Fairgoer2"), E("Fairgoer3", (6, 0, 10), "Fairgoer3"),
        ]);
    }

    private void BuildCastle()
    {
        PlaneModel("CastleGround", "CastleStone", 46, 36);
        Box("CastleWallLong", "Stone", 14, 2.2, 0.5);
        Box("CastleWallShort", "Stone", 0.5, 2.2, 10);
        Box("Throne", "Gold", 1.6, 2.2, 1.2);
        Box("Banner", "Banner", 1.4, 3.2, 0.2);
        Box("Desk", "Wood", 2.4, 1.0, 1.2);
        Box("Tomb", "Stone", 1.4, 1.0, 2.6);
        Box("Hearth", "Rock", 3.0, 2.0, 1.0);
        Box("Portrait", "Banner", 1.2, 1.6, 0.15);
        Box("PortraitGold", "Gold", 1.2, 1.6, 0.15);
        Box("PortraitBlue", "Cloth", 1.2, 1.6, 0.15);
        Box("PortraitPaper", "Paper", 1.2, 1.6, 0.15);
        Box("Gatehouse", "Stone", 3.0, 4.0, 2.0);

        Scene("Places/Castle",
        [
            .. StageEntities((0, 0, 13), projects: Echo),
            new Ent("Sun", (0, 5, 0), Rot: SunRotation, Comps: [Sun(3.5)]),
            E("Ambient", (0, 5, 0), null, Ambient(0.9)),
            new Ent("Area", Comps: [Area(21, 16, "line:place_castle")]),
            Spawn("Gate", (0, 0, 13)), Spawn("Arrival", (0, 0, 13)),
            new Ent("Ground", Model: "CastleGround"),
            E("GatehouseLeft", (-3.5, 0, 17), "Gatehouse"), E("GatehouseRight", (3.5, 0, 17), "Gatehouse"),
            E("HallWallLeft", (-8, 0, -9), "CastleWallShort"), E("HallWallRight", (8, 0, -9), "CastleWallShort"),
            E("LibraryWall", (-14, 0, -2), "CastleWallLong"), E("StudyWall", (14, 0, -2), "CastleWallLong"),
            E("ChapelWall", (-14, 0, 9), "CastleWallLong"), E("KitchenWall", (14, 0, 9), "CastleWallLong"),
            E("Throne", (0, 0, -14), "Throne"), E("Banner1", (-3, 0, -15.5), "Banner"), E("Banner2", (3, 0, -15.5), "Banner"),
            PersonEntity("Ysolde", (0, 0, -12)),
            PersonEntity("Ottilie", (-14, 0, -8)),
            Thing("Shelves", (-17, 0, -12.5), "Shelf", "line:use_look"),
            Thing("Desk", (14, 0, -10), "Desk", "line:use_look"),
            PersonEntity("Corvin", (11.5, 0, -6.5), comps: [Shown("$corvin_at_castle")]),
            Thing("Tomb", (-15, 0, 5), "Tomb", "line:use_look"),
            PersonEntity("Agathe", (15, 0, 5)), E("Hearth", (17.5, 0, 6.5), "Hearth"),
            Thing("PortraitFounder", (-4.5, 0, -1.2), "PortraitGold", "line:use_portrait"),
            Thing("PortraitTwins", (-1.5, 0, -1.2), "Portrait", "line:use_portrait"),
            Thing("PortraitSaint", (1.5, 0, -1.2), "PortraitBlue", "line:use_portrait"),
            Thing("PortraitHound", (4.5, 0, -1.2), "PortraitPaper", "line:use_portrait"),
            PersonEntity("Pell", (8, 0, 11.5), comps: [Shown("$pell_at_castle")]), E("PellsCart", (9.5, 0, 12.5), "Cart"),
            E("VillageRoad", (0, 0, 16.8), "Signpost", Door("Places/Village", "FromCastle", true, "line:go_village", "VillageRoad")),
            E("MineRoad", (-6, 0, 16.2), "Signpost", Door("Places/Mine", "Camp", true, "line:go_mine", "MineRoad")),
        ]);
    }

    private void BuildMine()
    {
        PlaneModel("MineGround", "Rock", 38, 30);
        Box("Headframe", "Wood", 3.0, 7.0, 3.0);
        Box("TallyBoard", "Wood", 2.0, 2.0, 0.2);
        Box("Tent", "Cloth", 3.0, 2.0, 3.0);
        Sphere("Campfire", "Fire", 0.5, (0, 0.3, 0));
        Box("Rubble", "Rock", 3.0, 1.6, 2.0);
        Box("Timber", "Wood", 0.4, 2.6, 0.4);
        PlaneModel("GalleryFloor", "Rock", 16, 12);
        PlaneModel("FloodWater", "Water", 12, 9);
        PlaneModel("DeepFloor", "Rock", 16, 12);
        Box("Carvings", "Stone", 3.0, 2.4, 0.4);
        Box("Seam", "Glow", 2.0, 0.6, 0.4);
        Sphere("EchoHole", "Dark", 0.9, (0, 0.9, 0));
        Box("Opening", "Dark", 1.6, 2.4, 0.2);

        Scene("Places/Mine",
        [
            .. StageEntities((0, 0, 10), lantern: true, projects: Echo),
            E("Ambient", (0, 5, 0), null, Ambient(0.45, (0.7, 0.75, 0.95))),
            new Ent("Moon", (0, 5, 0), Rot: SunRotation, Comps: [Sun(0.6, (0.7, 0.75, 1.0))]),
            new Ent("Area", Comps: [Area(17, 13, "line:place_mine")]),
            Spawn("Camp", (0, 0, 10)), Spawn("Arrival", (0, 0, 10)), Spawn("LiftTop", (-4, 0, -3.4)),
            new Ent("Ground", Model: "MineGround"),
            E("Headframe", (-4, 0, -8), "Headframe"), E("Tent1", (8, 0, 4), "Tent"), E("Tent2", (11, 0, 0), "Tent"),
            E("Campfire", (4, 0, 6), "Campfire", PointLight((1.0, 0.6, 0.3), 12, 6)),
            PersonEntity("Gudrun", (5.5, 0, 3)),
            E("Miner1", (2.5, 0, 7.5), "Miner1"), E("Miner2", (6, 0, 8), "Miner2"),
            E("CampTalk", (4.2, 0, 7.6), null, Runner("ChatRunner", Story, "Caption"), Caption("Caption"), Chatter("ChatRunner", "CampChatter", 8)),
            Thing("TallyBoard", (9, 0, -2), "TallyBoard", "line:use_tally"),
            E("Lift", (-4, 0, -5.8), "Doorway", Door("Places/Mine/Gallery", label: "line:go_down", node: "LiftDown")),
            E("VillageRoad", (0, 0, 14), "Signpost", Door("Places/Village", "FromMine", true, "line:go_village", "MineToVillage")),
        ], default, "Places/Mine/Gallery", "Places/Mine/Flooded", "Places/Mine/Deep");

        Scene("Places/Mine/Gallery",
        [
            new Ent("Area", Comps: [Area(7.5, 5.5, "line:place_gallery")]), new Ent("Floor", Model: "GalleryFloor"),
            E("Light1", (-4, 3, 0), null, PointLight((1.0, 0.8, 0.5), 9, 3)),
            Spawn("Arrival", (-6, 0, 0)), Spawn("FromFlooded", (5, 0, 0)), Spawn("FromDeep", (5, 0, -3.5)),
            E("Rubble", (1, 0, -1), "Rubble"), E("Timber1", (-2, 0, -3), "Timber"), E("Timber2", (3, 0, 3.5), "Timber"),
            PersonEntity("Corvin", (2.2, 0, 3.4), comps: [Shown("$corvin_in_mine")]),
            E("GalleryLift", (-7.4, 0, 0), "Doorway", Door("Places/Mine", "LiftTop", label: "line:go_up")),
            E("OldShaft", (7.4, 0, -4), "Opening", Door("Places/Mine/Deep", label: "line:go_shaft", node: "OldShaft")),
            E("FloodedPassage", (7.4, 0, 0), "Opening", Door("Places/Mine/Flooded", label: "line:go_water", node: "FloodedPassage")),
            E("LongWay", (7.4, 0, 4), "Opening", Door("Places/Mine/Deep", label: "line:go_long", node: "LongWay")),
        ], (200, 0, 0));

        Scene("Places/Mine/Flooded",
        [
            new Ent("Area", Comps: [Area(5.5, 4.0, "line:place_flooded")]), new Ent("Floor", Model: "GalleryFloor"), E("Water", (0, 0.4, 0), "FloodWater"),
            E("Light1", (0, 3, 0), null, PointLight((0.6, 0.75, 1.0), 9, 2)),
            Spawn("Arrival", (-5, 0, 0)),
            E("FloodedBack", (-6.2, 0, 0), "Opening", Door("Places/Mine/Gallery", "FromFlooded", label: "line:go_back")),
            E("FloodedOn", (6.2, 0, 0), "Opening", Door("Places/Mine/Deep", label: "line:go_on", node: "FloodedOn")),
        ], (300, 0, 0));

        Scene("Places/Mine/Deep",
        [
            new Ent("Area", Comps: [Area(7.5, 5.5, "line:place_deep")]), new Ent("Floor", Model: "DeepFloor"),
            E("SeamLight", (2, 2, -4), null, PointLight((0.45, 0.95, 0.85), 12, 6)),
            Spawn("Arrival", (-6, 0, 0)),
            PersonEntity("Jory", (2.5, 0, 2.5)),
            Thing("Carvings", (-1, 0, -5), "Carvings", "line:use_carvings"),
            E("Seam", (2.8, 1.4, -5), "Seam"),
            E("Echo", (5.6, 0, -3.6), "EchoHole",
                Runner("EchoRunner", Echo, "EchoLine", "EchoOptions", "EchoAdvancer"),
                LinePresenter("EchoLine"), OptionsPresenter("EchoOptions"), Advancer("EchoAdvancer"),
                Talkable("Echo", thing: true, label: "line:use_echo", runner: "EchoRunner")),
            E("DeepLift", (-7.4, 0, -3), "Doorway", Door("Places/Mine", "LiftTop", label: "line:go_up", node: "DeepLift")),
        ], (400, 0, 0));
    }

    // ================================================================ write

    public void Write()
    {
        foreach (var (name, color) in Materials)
        {
            WriteFile($"World/Materials/{name}.sdmat", MaterialText(name, color, null));
        }

        foreach (var (name, color, intensity) in Emissive)
        {
            WriteFile($"World/Materials/{name}.sdmat", MaterialText(name, color, intensity));
        }

        foreach (var (name, model) in models)
        {
            WriteFile($"World/Models/{name}.sdpromodel", ModelText(name, model));
        }

        // A child scene names its parent too: the editor keeps a child only when both sides agree
        var parents = scenes.SelectMany(s => s.Children.Select(c => (Child: c, Parent: s.Url))).ToDictionary(p => p.Child, p => p.Parent);
        foreach (var scene in scenes)
        {
            WriteFile(scene.Url + ".sdscene", SceneText(scene, parents.GetValueOrDefault(scene.Url)));
        }

        WriteFile("UI/Font.sdfnt", $$"""
            !SpriteFont
            Id: {{Uid("font")}}
            SerializedVersion: {Stride: 2.1.0.0}
            Tags: []
            FontSource: !SystemFontProvider
                FontName: Arial
                Style: Regular
            FontType: !RuntimeRasterizedSpriteFontType
                Size: 22.0

            """);

        Console.WriteLine($"{Materials.Length + Emissive.Length} materials, {models.Count} models, {scenes.Count} scenes");
        Console.WriteLine("scene ids:");
        foreach (var scene in scenes)
        {
            Console.WriteLine($"  {scene.Url}: {Uid("scene", scene.Url)}");
        }

        Console.WriteLine($"story project: {Uid("yarn", "Ashcombe")}  echo project: {Uid("yarn", "Echo")}  font: {Uid("font")}");
        Console.WriteLine($"compositor: {Uid("compositor")}  settings: {Uid("settings")}");
    }
}
