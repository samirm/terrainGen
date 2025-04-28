using System;
using System.Collections.Generic;
using UnityEngine;
using Random = System.Random;

// Needed for Lists and Dictionaries

public enum TileType
{
    Water,
    Grass,
    Mountain,
    Hill
}

public class HexSphereGenerator : MonoBehaviour
{
    // --- Public Fields (Editable in Inspector) ---
    [Header("Generation Settings")] [Range(0, 6)]
    public int subdivisionLevel = 2;

    public float radius = 5f;

    public int mapSeed;

    // --- Constants ---
    // Golden Ratio (phi) for icosahedron vertex calculation
    private readonly float _goldenRatio = (1f + Mathf.Sqrt(5f)) / 2f;

    // Base Icosahedron triangles (indices referencing icosahedronVertices array)
    // Defines the 20 initial triangular faces connecting the vertices
    private readonly int[] _icosahedronTriangles =
    {
        0, 1, 2,
        0, 3, 1,
        0, 2, 4,
        3, 0, 5,
        0, 4, 5,
        1, 3, 6,
        1, 7, 2,
        7, 1, 6,
        4, 2, 8,
        7, 8, 2,
        9, 3, 5,
        6, 3, 9,
        5, 4, 10,
        4, 8, 10,
        9, 5, 10,
        7, 6, 11,
        7, 11, 8,
        11, 6, 9,
        8, 11, 10,
        10, 11, 9
    };

    // Base Icosahedron vertices (normalized to unit sphere)
    // *** DECLARED HERE, INITIALIZED IN Awake() ***
    private Vector3[] _icosahedronVertices;
    private Dictionary<long, int> _midpointCache;
    private Random _pseudoRandomGen;

    // --- Data Structures ---
    private List<Vector3> _vertices;

    // Public property to access tiles if needed by other scripts
    public List<HexTile> Tiles { get; private set; }


    // --- Unity Methods ---

    // Called when the script instance is being loaded
    private void Awake()
    {
        // Initialize data that depends on instance fields first
        InitializeIcosahedronData();

        // Now generate the sphere using the initialized data
        Generate();
    }

    // Start is called before the first frame update (Generate is now called in Awake)
    // void Start() { }

    private void OnDrawGizmos()
    {
        if (Tiles == null) return;

        for (var i = 0; i < Tiles.Count; i++)
        {
            var tile = Tiles[i];
            if (tile == null || tile.cornerVertices == null || tile.cornerVertices.Count < 3) continue;

            // --- Draw Center (Optional) ---
            var colorIntensity = Mathf.Clamp01(tile.heightLevel);
            var baseColor = tile.isPentagon ? Color.red : Color.blue;
            Gizmos.color = Color.Lerp(baseColor * 0.5f, baseColor, colorIntensity);
            // Gizmos.DrawSphere(tile.CenterPosition, 0.05f * radius * 0.1f); // Can hide this now

            // --- Draw Tile Outline ---
            Gizmos.color = Color.yellow; // Use a distinct color for outlines
            for (var j = 0; j < tile.cornerVertices.Count; j++)
            {
                var currentCorner = tile.cornerVertices[j];
                // Get the next corner, wrapping around using the modulo operator (%)
                var nextCorner = tile.cornerVertices[(j + 1) % tile.cornerVertices.Count];
                Gizmos.DrawLine(currentCorner, nextCorner);
            }

            /* // --- Optional: Draw Neighbor Connections (Keep commented for performance) ---
            Gizmos.color = Color.white * 0.5f;
            // ... (neighbor drawing code remains the same) ...
            */
        }
    }

    private void InitializeIcosahedronData()
    {
        _icosahedronVertices = new Vector3[]
        {
            new(0.8506508f, 0.5257311f, 0f), // 0
            new(0.000000101405476f, 0.8506507f, -0.525731f), // 1
            new(0.000000101405476f, 0.8506506f, 0.525731f), // 2
            new(0.5257309f, -0.00000006267203f, -0.85065067f), // 3
            new(0.52573115f, -0.00000006267203f, 0.85065067f), // 4
            new(0.8506508f, -0.5257311f, 0f), // 5
            new(-0.52573115f, 0.00000006267203f, -0.85065067f), // 6
            new(-0.8506508f, 0.5257311f, 0f), // 7
            new(-0.5257309f, 0.00000006267203f, 0.85065067f), // 8
            new(-0.000000101405476f, -0.8506506f, -0.525731f), // 9
            new(-0.000000101405476f, -0.8506507f, 0.525731f), // 10
            new(-0.8506508f, -0.5257311f, 0f) // 11
        };
        // Ensure icosahedronTriangles has exactly 60 indices (20 triangles * 3 vertices)
        if (_icosahedronTriangles.Length != 60)
        {
            Debug.LogError($"Icosahedron triangle definition is incorrect! Expected 60 indices, " +
                           $"found {_icosahedronTriangles.Length}. Check for duplicates or missing faces.");
            throw new Exception("Icosahedron triangle definition is incorrect!");
        }
    }

    public void Generate()
    {
        _pseudoRandomGen = new Random(mapSeed);
        if (_icosahedronVertices == null)
        {
            Debug.LogError("Icosahedron data not initialized before Generate() call!");
            InitializeIcosahedronData();
        }

        _vertices = new List<Vector3>(_icosahedronVertices);
        _midpointCache = new Dictionary<long, int>();
        var currentTriangles = new List<int>(_icosahedronTriangles);

        for (var i = 0; i < subdivisionLevel; i++)
        {
            var newTriangles = new List<int>();
            for (var j = 0; j < currentTriangles.Count; j += 3)
            {
                var v1 = currentTriangles[j];
                var v2 = currentTriangles[j + 1];
                var v3 = currentTriangles[j + 2];

                var m12 = GetMidpointIndex(v1, v2);
                var m23 = GetMidpointIndex(v2, v3);
                var m31 = GetMidpointIndex(v3, v1);

                newTriangles.AddRange(new[] { v1, m12, m31 });
                newTriangles.AddRange(new[] { v2, m23, m12 });
                newTriangles.AddRange(new[] { v3, m31, m23 });
                newTriangles.AddRange(new[] { m12, m23, m31 });
            }

            currentTriangles = newTriangles;
        }

        var finalTriangles = currentTriangles;

        for (var i = 0; i < _vertices.Count; i++) _vertices[i] = _vertices[i].normalized * radius;

        CreateTiles();
        FindNeighbors(finalTriangles);
        CalculateTileCorners(finalTriangles);

        Debug.Log(
            $"Generated Hexasphere: {Tiles.Count} tiles. (Subdivision level: {subdivisionLevel}, Seed: {mapSeed})");
    }

    private int GetMidpointIndex(int v1, int v2)
    {
        long smallerIndex = Mathf.Min(v1, v2);
        long greaterIndex = Mathf.Max(v1, v2);
        var key = (smallerIndex << 32) + greaterIndex;

        if (_midpointCache.TryGetValue(key, out var ret)) return ret;

        var p1 = _vertices[v1];
        var p2 = _vertices[v2];
        var midpoint = (p1 + p2) * 0.5f;
        var newIndex = _vertices.Count;
        _vertices.Add(midpoint);
        _midpointCache.Add(key, newIndex);

        return newIndex;
    }

    private void CreateTiles()
    {
        Tiles = new List<HexTile>(_vertices.Count);
        _pseudoRandomGen ??= new Random(mapSeed);

        for (var i = 0; i < _vertices.Count; i++)
        {
            var isPentagon = i < 12;
            var newTile = new HexTile(i, _vertices[i], isPentagon)
            {
                heightLevel = _pseudoRandomGen.Next(-3, 4)
            };
            var randomTypeValue = (float)_pseudoRandomGen.NextDouble();

            newTile.type = newTile.heightLevel switch
            {
                < 0 => TileType.Water, // Negative levels are always Water
                1 => TileType.Grass,
                2 => TileType.Hill,
                3 => TileType.Mountain,
                _ => TileType.Grass
            };

            Tiles.Add(newTile);
        }
    }

    private void FindNeighbors(List<int> finalTriangles)
    {
        var neighborSets = new Dictionary<int, HashSet<int>>();
        for (var i = 0; i < Tiles.Count; i++) neighborSets.Add(i, new HashSet<int>());

        for (var i = 0; i < finalTriangles.Count; i += 3)
        {
            var v1 = finalTriangles[i];
            var v2 = finalTriangles[i + 1];
            var v3 = finalTriangles[i + 2];

            // Safety check for valid indices before accessing neighborSets
            if (v1 < 0 || v1 >= Tiles.Count || v2 < 0 || v2 >= Tiles.Count || v3 < 0 || v3 >= Tiles.Count)
            {
                Debug.LogWarning(
                    $"Invalid vertex index found in triangle data at index {i}. Skipping neighbor assignment for this triangle.");
                continue;
            }

            neighborSets[v1].Add(v2);
            neighborSets[v2].Add(v1);
            neighborSets[v1].Add(v3);
            neighborSets[v3].Add(v1);
            neighborSets[v2].Add(v3);
            neighborSets[v3].Add(v2);
        }

        foreach (var kvp in neighborSets)
            if (kvp.Key >= 0 && kvp.Key < Tiles.Count && Tiles[kvp.Key] != null)
                Tiles[kvp.Key].neighborIds.AddRange(kvp.Value);
    }

    // Calculates the corner vertices for each tile based on shared triangle centroids
    private void CalculateTileCorners(List<int> finalTriangles)
    {
        // Temporary dictionary to gather corners for each tile ID
        var cornersByTileId = new Dictionary<int, List<Vector3>>();
        for (var i = 0; i < Tiles.Count; i++) cornersByTileId.Add(i, new List<Vector3>());

        // Each triangle in the final mesh defines a corner vertex
        for (var i = 0; i < finalTriangles.Count; i += 3)
        {
            var v1Idx = finalTriangles[i];
            var v2Idx = finalTriangles[i + 1];
            var v3Idx = finalTriangles[i + 2];

            // Get the positions of the tile centers forming this triangle
            var p1 = _vertices[v1Idx];
            var p2 = _vertices[v2Idx];
            var p3 = _vertices[v3Idx];

            // Calculate the centroid of the triangle
            var centroid = (p1 + p2 + p3) / 3.0f;

            // Project the centroid onto the sphere surface to get the corner position
            var corner = centroid.normalized * radius;

            // This corner belongs to the tiles centered at v1, v2, and v3
            // Add it to the list for each of these tiles in our temporary dictionary
            if (v1Idx < Tiles.Count) cornersByTileId[v1Idx].Add(corner);
            if (v2Idx < Tiles.Count) cornersByTileId[v2Idx].Add(corner);
            if (v3Idx < Tiles.Count) cornersByTileId[v3Idx].Add(corner);
        }

        // Now, assign the collected corners to the actual HexTile objects
        // And crucially, order them clockwise/counter-clockwise
        foreach (var kvp in cornersByTileId)
        {
            var tileId = kvp.Key;
            var collectedCorners = kvp.Value;

            if (tileId >= 0 && tileId < Tiles.Count && Tiles[tileId] != null)
                // Order the corners before assigning them
                Tiles[tileId].cornerVertices = OrderCornersClockwise(Tiles[tileId].centerPosition, collectedCorners);
        }
    }

    // Helper function to order corner vertices around a center point
    private List<Vector3> OrderCornersClockwise(Vector3 center, List<Vector3> corners)
    {
        if (corners == null || corners.Count < 3) return corners; // Not enough corners to order

        // Calculate the normal vector at the center of the tile (approximates the 'up' direction)
        var normal = center.normalized;

        // Use List.Sort with a custom comparer
        corners.Sort((a, b) =>
        {
            // Project vectors onto the plane perpendicular to the normal
            var dirA = (a - center).normalized; // Direction from center to corner A
            var dirB = (b - center).normalized; // Direction from center to corner B

            // Choose an arbitrary reference direction on the tangent plane (e.g., cross product with 'up')
            // Ensure the reference vector is not parallel to the normal
            var referenceDirection = Vector3.Cross(normal, Vector3.up);
            if (referenceDirection.sqrMagnitude < 0.001f)
                referenceDirection = Vector3.Cross(normal, Vector3.right); // Fallback if normal is vertical
            referenceDirection.Normalize();


            // Calculate signed angles relative to the reference direction around the normal axis
            var angleA = Vector3.SignedAngle(referenceDirection, dirA, normal);
            var angleB = Vector3.SignedAngle(referenceDirection, dirB, normal);

            // Compare angles (handle wrap around 360 if necessary, though SignedAngle usually handles this from -180 to 180)
            return angleA.CompareTo(angleB);
        });

        return corners;
    }


    // --- Tile Data Class ---
    [Serializable]
    public class HexTile
    {
        public int id;
        public Vector3 centerPosition;
        public List<int> neighborIds;
        public bool isPentagon;
        public int heightLevel;
        public List<Vector3> cornerVertices;
        public TileType type;

        public HexTile(int id, Vector3 center, bool isPent)
        {
            this.id = id;
            centerPosition = center;
            isPentagon = isPent;
            neighborIds = new List<int>();
            cornerVertices = new List<Vector3>();
            type = TileType.Grass;
        }
    }
}