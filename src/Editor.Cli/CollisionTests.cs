using System.Numerics;
using BepuPhysics.Collidables;
using BepuUtilities.Memory;
using StbEditor.Nif;
using StbEditor.Render;

internal static class CollisionTests
{
    public static void Run()
    {
        Vector3[] cube = [new(-1,-1,-1), new(1,-1,-1), new(-1,1,-1), new(1,1,-1),
            new(-1,-1,1), new(1,-1,1), new(-1,1,1), new(1,1,1)];
        Check(cube, "cube", expectedEdges: 12);
        Check(CycleRegression(), "near-coplanar face-cycle regression");
        Console.WriteLine("collisiontest: PASS: cube edges, hull bounds, unique finite edges and positive inertia");
    }

    private static void Check(Vector3[] points, string name, int? expectedEdges = null)
    {
        var model = new NifModel { Path = name };
        model.CollisionPieces.Add(points);
        var lines = MeshCache.CollisionLines(model);
        Require(lines.Length >= 12 && lines.Length % 2 == 0, name + ": incomplete collision edges");
        if (expectedEdges is { } count) Require(lines.Length == count * 2, name + ": wrong hull edge count");
        var source = points.ToHashSet();
        var edges = new HashSet<(Vector3 A, Vector3 B)>();
        var low = new Vector3(float.MaxValue);
        var high = new Vector3(float.MinValue);
        for (var i = 0; i < lines.Length; i += 2)
        {
            var a = lines[i];
            var b = lines[i + 1];
            Require(Finite(a) && Finite(b) && a != b && source.Contains(a) && source.Contains(b), name + ": invalid collision edge");
            var order = a.X.CompareTo(b.X);
            if (order == 0) order = a.Y.CompareTo(b.Y);
            if (order == 0) order = a.Z.CompareTo(b.Z);
            Require(edges.Add(order < 0 ? (a, b) : (b, a)), name + ": duplicate collision edge");
            low = Vector3.Min(low, Vector3.Min(a, b));
            high = Vector3.Max(high, Vector3.Max(a, b));
        }
        var sourceLow = points.Aggregate(new Vector3(float.MaxValue), Vector3.Min);
        var sourceHigh = points.Aggregate(new Vector3(float.MinValue), Vector3.Max);
        Require(Vector3.Distance(low, sourceLow) < 0.001f && Vector3.Distance(high, sourceHigh) < 0.001f, name + ": lost hull extrema");
        var pool = new BufferPool();
        try
        {
            var hull = new ConvexHull(points.AsSpan(), pool, out var center);
            try
            {
                var t = hull.ComputeInertia(1).InverseInertiaTensor;
                var minor = (double)t.XX * t.YY - (double)t.YX * t.YX;
                var determinant = (double)t.XX * ((double)t.YY * t.ZZ - (double)t.ZY * t.ZY)
                    - (double)t.YX * ((double)t.YX * t.ZZ - (double)t.ZY * t.ZX)
                    + (double)t.ZX * ((double)t.YX * t.ZY - (double)t.YY * t.ZX);
                Require(Finite(center) && t.XX > 0 && minor > 0 && double.IsFinite(determinant) && determinant > 0,
                    name + ": invalid dynamic-body inertia");
            }
            finally { hull.Dispose(pool); }
        }
        finally { pool.Clear(); }
        Console.WriteLine($"collisiontest: {name}: {edges.Count} unique edges; bounds and inertia valid");
    }

    private static bool Finite(Vector3 p) => float.IsFinite(p.X) && float.IsFinite(p.Y) && float.IsFinite(p.Z);
    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }

    // Public reproducer for numerical face-walk cycles: https://github.com/bepu/bepuphysics2/issues/325
    // Keep the source coordinates unchanged; rounding/merging can hide the original failure.
    private static Vector3[] CycleRegression() =>
    [
        new(-0.637357891f, 0.347849399f, -0.303436399f),
        new(-0.636290252f, 0.345867455f, -0.301366687f),
        new(-0.992014945f, 0.348357588f, -0.3031407f),
        new(-1.00909662f, 0.386065364f, -0.303337872f),
        new(0.637357891f, 0.347849399f, -0.303436399f),
        new(-0.636290252f, 0.345918268f, 0.701366544f),
        new(-0.636503756f, 0.345918268f, 0.700873733f),
        new(-0.992655516f, 0.346578926f, 0.701070845f),
        new(-0.992655516f, 0.346578926f, -0.301070988f),
        new(0.636290252f, 0.345867455f, -0.301366687f),
        new(-0.995858312f, 0.348510057f, -0.301859498f),
        new(-1.01272643f, 0.385912925f, -0.302056611f),
        new(-1.01037765f, 0.390029252f, -0.302746475f),
        new(-0.637357891f, 0.389521062f, -0.302845061f),
        new(1.00909662f, 0.386065364f, -0.303337872f),
        new(0.992014945f, 0.348357588f, -0.3031407f),
        new(-0.637357891f, 0.347849399f, 0.703436255f),
        new(-0.992014945f, 0.348357588f, 0.703140557f),
        new(0.636290252f, 0.345918268f, 0.701366544f),
        new(-0.995858312f, 0.348510057f, 0.701859355f),
        new(-1.02553761f, 0.351406753f, 0.678599536f),
        new(-1.0251106f, 0.35013628f, 0.675938487f),
        new(-1.0251106f, 0.35013628f, -0.2759386f),
        new(-1.02553761f, 0.351406753f, -0.278599679f),
        new(0.992655516f, 0.346578926f, -0.301070988f),
        new(0.992655516f, 0.346578926f, 0.701070845f),
        new(0.636503756f, 0.345918268f, 0.700873733f),
        new(-1.04432738f, 0.37869662f, -0.274558783f),
        new(-1.01400757f, 0.389673531f, -0.301465213f),
        new(-1.04582202f, 0.382304758f, -0.273770332f),
        new(-1.0582062f, 0.67344743f, -0.220745891f),
        new(-1.0545764f, 0.674260557f, -0.22183004f),
        new(-0.637144327f, 0.674158931f, -0.221928596f),
        new(1.01037765f, 0.390029252f, -0.302746475f),
        new(0.637357891f, 0.389521062f, -0.302845061f),
        new(1.01272643f, 0.385912925f, -0.302056611f),
        new(0.995858312f, 0.348510057f, -0.301859498f),
        new(-1.00909662f, 0.386065364f, 0.703337729f),
        new(0.637357891f, 0.347849399f, 0.703436255f),
        new(0.992014945f, 0.348357588f, 0.703140557f),
        new(-1.01272643f, 0.385912925f, 0.702056468f),
        new(-1.04432738f, 0.37869662f, 0.67455864f),
        new(-1.04582202f, 0.380170345f, 0.671404779f),
        new(-1.04582202f, 0.380170345f, -0.271404922f),
        new(1.02553761f, 0.351406753f, -0.278599679f),
        new(1.0251106f, 0.35013628f, -0.2759386f),
        new(1.0251106f, 0.35013628f, 0.675938487f),
        new(1.02553761f, 0.351406753f, 0.678599536f),
        new(0.995858312f, 0.348510057f, 0.701859355f),
        new(-1.08980727f, 0.656575501f, -0.196303427f),
        new(-1.09023428f, 0.656982064f, -0.193346679f),
        new(-1.0584197f, 0.67675066f, -0.21867618f),
        new(-1.0550034f, 0.677512944f, -0.219858885f),
        new(-1.09023428f, 0.659827888f, -0.194233686f),
        new(0.637144327f, 0.674158931f, -0.221928596f),
        new(1.01400757f, 0.389673531f, -0.301465213f),
        new(1.0545764f, 0.674260557f, -0.22183004f),
        new(1.0582062f, 0.67344743f, -0.220745891f),
        new(1.04582202f, 0.382304758f, -0.273770332f),
        new(1.04432738f, 0.37869662f, -0.274558783f),
        new(-0.637357891f, 0.389521062f, 0.702844918f),
        new(-1.01037765f, 0.390029252f, 0.702746332f),
        new(1.00909662f, 0.386065364f, 0.703337729f),
        new(-1.01400757f, 0.389673531f, 0.70146507f),
        new(-1.04582202f, 0.382304758f, 0.673770189f),
        new(-1.09023428f, 0.656982064f, 0.593346536f),
        new(1.04582202f, 0.380170345f, -0.271404922f),
        new(1.04582202f, 0.380170345f, 0.671404779f),
        new(1.04432738f, 0.37869662f, 0.67455864f),
        new(1.01272643f, 0.385912925f, 0.702056468f),
        new(-1.09066129f, 0.832155526f, 0.199999928f),
        new(-1.0584197f, 0.86234206f, -0.0161386579f),
        new(-1.0550034f, 0.863663316f, -0.0167300105f),
        new(1.0550034f, 0.677512944f, -0.219858885f),
        new(-1.09023428f, 0.833781719f, -0.00135488808f),
        new(1.08980727f, 0.656575501f, -0.196303427f),
        new(1.0584197f, 0.67675066f, -0.21867618f),
        new(1.09023428f, 0.659827888f, -0.194233686f),
        new(1.09023428f, 0.656982064f, -0.193346679f),
        new(0.637357891f, 0.389521062f, 0.702844918f),
        new(1.01037765f, 0.390029252f, 0.702746332f),
        new(-0.637144327f, 0.674158931f, 0.621928453f),
        new(-1.0545764f, 0.674260557f, 0.621829867f),
        new(-1.0582062f, 0.67344743f, 0.620745778f),
        new(-1.08980727f, 0.656575501f, 0.596303284f),
        new(-1.09023428f, 0.659827888f, 0.594233513f),
        new(1.04582202f, 0.382304758f, 0.673770189f),
        new(1.09023428f, 0.656982064f, 0.593346536f),
        new(1.01400757f, 0.389673531f, 0.70146507f),
        new(-1.09044778f, 0.834950566f, 0.199999928f),
        new(-1.09023428f, 0.833781719f, 0.40135473f),
        new(-1.0584197f, 0.863663316f, -0.0124919862f),
        new(-1.0550034f, 0.865035474f, -0.0132804662f),
        new(1.0550034f, 0.863663316f, -0.0167300105f),
        new(-1.09002078f, 0.835204661f, 0.00219321251f),
        new(1.0584197f, 0.86234206f, -0.0161386579f),
        new(1.09023428f, 0.833781719f, -0.00135488808f),
        new(1.09066129f, 0.832155526f, 0.199999928f),
        new(1.0582062f, 0.67344743f, 0.620745778f),
        new(1.0545764f, 0.674260557f, 0.621829867f),
        new(0.637144327f, 0.674158931f, 0.621928453f),
        new(-1.0550034f, 0.677512944f, 0.619858742f),
        new(-1.0584197f, 0.67675066f, 0.618676066f),
        new(-1.0584197f, 0.86234206f, 0.41613853f),
        new(1.08980727f, 0.656575501f, 0.596303284f),
        new(1.09023428f, 0.659827888f, 0.594233513f),
        new(-1.09002078f, 0.835204661f, 0.397806644f),
        new(-1.05863321f, 0.863612533f, 0.199999928f),
        new(-1.0584197f, 0.863663316f, 0.412491858f),
        new(-1.0550034f, 0.865035474f, 0.413280308f),
        new(1.0550034f, 0.865035474f, -0.0132804662f),
        new(1.0584197f, 0.863663316f, -0.0124919862f),
        new(1.09002078f, 0.835204661f, 0.00219321251f),
        new(1.09044778f, 0.834950566f, 0.199999928f),
        new(1.09023428f, 0.833781719f, 0.40135473f),
        new(1.0584197f, 0.67675066f, 0.618676066f),
        new(1.0550034f, 0.677512944f, 0.619858742f),
        new(-1.0550034f, 0.863663316f, 0.416729867f),
        new(1.0584197f, 0.86234206f, 0.41613853f),
        new(1.0550034f, 0.865035474f, 0.413280308f),
        new(1.05863321f, 0.863612533f, 0.199999928f),
        new(1.09002078f, 0.835204661f, 0.397806644f),
        new(1.0584197f, 0.863663316f, 0.412491858f),
        new(1.0550034f, 0.8636633f, 0.41672987f),
    ];
}
