using UnityEngine;
using System.Collections.Generic; // Needed for Lists and Dictionaries

public class HexasphereGenerator : MonoBehaviour
{
    // --- Constants ---
    // Golden Ratio (phi) for icosahedron vertex calculation
    private readonly float GoldenRatio = (1f + Mathf.Sqrt(5f)) / 2f;

    // Base Icosahedron vertices (normalized to unit sphere)
    // *** DECLARED HERE, INITIALIZED IN Awake() ***
    private Vector3[] icosahedronVertices;

    // Base Icosahedron triangles (indices referencing icosahedronVertices array)
    // Defines the 20 initial triangular faces connecting the vertices
    private readonly int[] icosahedronTriangles = {
        0,  1,  2,
        0,  3,  1,
        0,  2,  4,
        3,  0,  5,
        0,  4,  5,
        1,  3,  6,
        1,  7,  2,
        7,  1,  6,
        4,  2,  8,
        7,  8,  2,
        9,  3,  5,
        6,  3,  9,
        5,  4, 10,
        4,  8, 10,
        9,  5, 10,
        7,  6, 11,
        7, 11,  8,
        11,  6,  9,
        8, 11, 10,
        10, 11,  9
    };

    // --- Public Fields (Editable in Inspector) ---
    [Header("Generation Settings")]
    [Range(0, 6)]
    public int subdivisionLevel = 2;
    public float radius = 5f;
    public int mapSeed = 0; // Added seed placeholder

    // --- Data Structures ---
    private List<Vector3> vertices;
    private List<HexTile> tiles;
    private Dictionary<long, int> midpointCache;

    // Public property to access tiles if needed by other scripts
    public List<HexTile> Tiles => tiles;


    // --- Tile Data Class ---
    [System.Serializable]
    public class HexTile
    {
        public int Id;
        public Vector3 CenterPosition;
        public List<int> NeighborIds;
        public bool IsPentagon;
        public float height = 0f; // Added height placeholder
        public List<Vector3> CornerVertices;

        public HexTile(int id, Vector3 center, bool isPent)
        {
            Id = id;
            CenterPosition = center;
            IsPentagon = isPent;
            NeighborIds = new List<int>();
            CornerVertices = new List<Vector3>();
        }
    }


    // --- Unity Methods ---

    // Called when the script instance is being loaded
    void Awake()
    {
        // Initialize data that depends on instance fields first
        InitializeIcosahedronData();

        // Now generate the sphere using the initialized data
        Generate();
    }

    // Start is called before the first frame update (Generate is now called in Awake)
    // void Start() { }

    void OnDrawGizmos()
    {
        if (tiles == null) return;

        for (int i = 0; i < tiles.Count; i++)
        {
            HexTile tile = tiles[i];
            if (tile == null || tile.CornerVertices == null || tile.CornerVertices.Count < 3) continue;

            // --- Draw Center (Optional) ---
            float colorIntensity = Mathf.Clamp01(tile.height);
            Color baseColor = tile.IsPentagon ? Color.red : Color.blue;
            Gizmos.color = Color.Lerp(baseColor * 0.5f, baseColor, colorIntensity);
            // Gizmos.DrawSphere(tile.CenterPosition, 0.05f * radius * 0.1f); // Can hide this now

            // --- Draw Tile Outline ---
            Gizmos.color = Color.yellow; // Use a distinct color for outlines
            for (int j = 0; j < tile.CornerVertices.Count; j++)
            {
                Vector3 currentCorner = tile.CornerVertices[j];
                // Get the next corner, wrapping around using the modulo operator (%)
                Vector3 nextCorner = tile.CornerVertices[(j + 1) % tile.CornerVertices.Count];
                Gizmos.DrawLine(currentCorner, nextCorner);
            }

            /* // --- Optional: Draw Neighbor Connections (Keep commented for performance) ---
            Gizmos.color = Color.white * 0.5f;
            // ... (neighbor drawing code remains the same) ...
            */
        }
    }

    // --- Initialization ---

    // *** NEW METHOD TO INITIALIZE ICOSAHEDRON DATA ***
    private void InitializeIcosahedronData()
    {
        icosahedronVertices = new Vector3[] {
            new(0.8506508f,           0.5257311f,         0f),            // 0
            new(0.000000101405476f,   0.8506507f,        -0.525731f),     // 1
            new(0.000000101405476f,   0.8506506f,         0.525731f),     // 2
            new(0.5257309f,          -0.00000006267203f, -0.85065067f),   // 3
            new(0.52573115f,         -0.00000006267203f,  0.85065067f),   // 4
            new(0.8506508f,          -0.5257311f,         0f),            // 5
            new(-0.52573115f,         0.00000006267203f, -0.85065067f),   // 6
            new(-0.8506508f,          0.5257311f,         0f),            // 7
            new(-0.5257309f,          0.00000006267203f,  0.85065067f),   // 8
            new(-0.000000101405476f, -0.8506506f,        -0.525731f),     // 9
            new(-0.000000101405476f, -0.8506507f,         0.525731f),     // 10
            new(-0.8506508f,         -0.5257311f,         0f)             // 11
        };
        // Ensure icosahedronTriangles has exactly 60 indices (20 triangles * 3 vertices)
        if(icosahedronTriangles.Length != 60) {
            Debug.LogError($"Icosahedron triangle definition is incorrect! Expected 60 indices, found {icosahedronTriangles.Length}. Check for duplicates or missing faces.");
        }
    }


    // --- Generation Logic ---

    // Main generation function (now called from Awake)
    public void Generate()
    {
        // Ensure base data is initialized (should be, as Awake runs first)
        if (icosahedronVertices == null)
        {
           Debug.LogError("Icosahedron data not initialized before Generate() call!");
           InitializeIcosahedronData(); // Attempt recovery
        }

        vertices = new List<Vector3>(icosahedronVertices);
        midpointCache = new Dictionary<long, int>();
        List<int> currentTriangles = new List<int>(icosahedronTriangles);

        for (int i = 0; i < subdivisionLevel; i++)
        {
            List<int> newTriangles = new List<int>();
            for (int j = 0; j < currentTriangles.Count; j += 3)
            {
                int v1 = currentTriangles[j];
                int v2 = currentTriangles[j + 1];
                int v3 = currentTriangles[j + 2];

                int m12 = GetMidpointIndex(v1, v2);
                int m23 = GetMidpointIndex(v2, v3);
                int m31 = GetMidpointIndex(v3, v1);

                newTriangles.AddRange(new int[] { v1, m12, m31 });
                newTriangles.AddRange(new int[] { v2, m23, m12 });
                newTriangles.AddRange(new int[] { v3, m31, m23 });
                newTriangles.AddRange(new int[] { m12, m23, m31 });
            }
            currentTriangles = newTriangles;
        }

        List<int> finalTriangles = currentTriangles;

        for (int i = 0; i < vertices.Count; i++)
        {
            vertices[i] = vertices[i].normalized * radius;
        }

        CreateTiles();
        FindNeighbors(finalTriangles);
        CalculateTileCorners(finalTriangles);

        Debug.Log($"Generated Hexasphere: {tiles.Count} tiles. (Subdivision level: {subdivisionLevel}, Seed: {mapSeed})");
    }

    private int GetMidpointIndex(int v1, int v2)
    {
        long smallerIndex = Mathf.Min(v1, v2);
        long greaterIndex = Mathf.Max(v1, v2);
        long key = (smallerIndex << 32) + greaterIndex;

        if (midpointCache.TryGetValue(key, out int ret))
        {
            return ret;
        }

        Vector3 p1 = vertices[v1];
        Vector3 p2 = vertices[v2];
        Vector3 midpoint = (p1 + p2) * 0.5f;
        int newIndex = vertices.Count;
        vertices.Add(midpoint);
        midpointCache.Add(key, newIndex);

        return newIndex;
    }

    private void CreateTiles()
    {
        tiles = new List<HexTile>(vertices.Count);
        for (int i = 0; i < vertices.Count; i++)
        {
            bool isPentagon = i < 12;
            var newTile = new HexTile(i, vertices[i], isPentagon);

            // Example placeholder height: taller near poles (normalized Y)
            // Ensure vertices are normalized before accessing y for consistent 0-1 range
            newTile.height = Mathf.Abs(vertices[i].normalized.y);

            tiles.Add(newTile);
        }
    }

     private void FindNeighbors(List<int> finalTriangles)
    {
        Dictionary<int, HashSet<int>> neighborSets = new Dictionary<int, HashSet<int>>();
        for(int i = 0; i < tiles.Count; i++)
        {
            neighborSets.Add(i, new HashSet<int>());
        }

        for (int i = 0; i < finalTriangles.Count; i += 3)
        {
            int v1 = finalTriangles[i];
            int v2 = finalTriangles[i + 1];
            int v3 = finalTriangles[i + 2];

             // Safety check for valid indices before accessing neighborSets
            if (v1 < 0 || v1 >= tiles.Count || v2 < 0 || v2 >= tiles.Count || v3 < 0 || v3 >= tiles.Count) {
                 Debug.LogWarning($"Invalid vertex index found in triangle data at index {i}. Skipping neighbor assignment for this triangle.");
                 continue;
            }

            neighborSets[v1].Add(v2); neighborSets[v2].Add(v1);
            neighborSets[v1].Add(v3); neighborSets[v3].Add(v1);
            neighborSets[v2].Add(v3); neighborSets[v3].Add(v2);
        }

        foreach(var kvp in neighborSets)
        {
            if(kvp.Key >= 0 && kvp.Key < tiles.Count && tiles[kvp.Key] != null)
            {
                tiles[kvp.Key].NeighborIds.AddRange(kvp.Value);
            }
        }
    }
     
     // Calculates the corner vertices for each tile based on shared triangle centroids
    private void CalculateTileCorners(List<int> finalTriangles)
    {
        // Temporary dictionary to gather corners for each tile ID
        Dictionary<int, List<Vector3>> cornersByTileId = new Dictionary<int, List<Vector3>>();
        for (int i = 0; i < tiles.Count; i++)
        {
            cornersByTileId.Add(i, new List<Vector3>());
        }

        // Each triangle in the final mesh defines a corner vertex
        for (int i = 0; i < finalTriangles.Count; i += 3)
        {
            int v1Idx = finalTriangles[i];
            int v2Idx = finalTriangles[i + 1];
            int v3Idx = finalTriangles[i + 2];

            // Get the positions of the tile centers forming this triangle
            Vector3 p1 = vertices[v1Idx];
            Vector3 p2 = vertices[v2Idx];
            Vector3 p3 = vertices[v3Idx];

            // Calculate the centroid of the triangle
            Vector3 centroid = (p1 + p2 + p3) / 3.0f;

            // Project the centroid onto the sphere surface to get the corner position
            Vector3 corner = centroid.normalized * radius;

            // This corner belongs to the tiles centered at v1, v2, and v3
            // Add it to the list for each of these tiles in our temporary dictionary
            if (v1Idx < tiles.Count) cornersByTileId[v1Idx].Add(corner);
            if (v2Idx < tiles.Count) cornersByTileId[v2Idx].Add(corner);
            if (v3Idx < tiles.Count) cornersByTileId[v3Idx].Add(corner);
        }

        // Now, assign the collected corners to the actual HexTile objects
        // And crucially, order them clockwise/counter-clockwise
        foreach (var kvp in cornersByTileId)
        {
            int tileId = kvp.Key;
            List<Vector3> collectedCorners = kvp.Value;

            if (tileId >= 0 && tileId < tiles.Count && tiles[tileId] != null)
            {
                // Order the corners before assigning them
                tiles[tileId].CornerVertices = OrderCornersClockwise(tiles[tileId].CenterPosition, collectedCorners);
            }
        }
    }

    // Helper function to order corner vertices around a center point
    private List<Vector3> OrderCornersClockwise(Vector3 center, List<Vector3> corners)
    {
        if (corners == null || corners.Count < 3)
        {
            return corners; // Not enough corners to order
        }

        // Calculate the normal vector at the center of the tile (approximates the 'up' direction)
        Vector3 normal = center.normalized;

        // Use List.Sort with a custom comparer
        corners.Sort((a, b) => {
            // Project vectors onto the plane perpendicular to the normal
            Vector3 dirA = (a - center).normalized; // Direction from center to corner A
            Vector3 dirB = (b - center).normalized; // Direction from center to corner B

            // Choose an arbitrary reference direction on the tangent plane (e.g., cross product with 'up')
            // Ensure the reference vector is not parallel to the normal
            Vector3 referenceDirection = Vector3.Cross(normal, Vector3.up);
            if (referenceDirection.sqrMagnitude < 0.001f)
            {
                referenceDirection = Vector3.Cross(normal, Vector3.right); // Fallback if normal is vertical
            }
            referenceDirection.Normalize();


            // Calculate signed angles relative to the reference direction around the normal axis
            float angleA = Vector3.SignedAngle(referenceDirection, dirA, normal);
            float angleB = Vector3.SignedAngle(referenceDirection, dirB, normal);

            // Compare angles (handle wrap around 360 if necessary, though SignedAngle usually handles this from -180 to 180)
            return angleA.CompareTo(angleB);
        });

        return corners;
    }
}
