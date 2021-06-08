
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using path;
using UnityEngine.Tilemaps;
using UnityEngine.UI;
using UnityEngine.UIElements;
using Random = UnityEngine.Random;

public class CellularAutomata : MonoBehaviour
{
    [SerializeField] private CellBehavior cellBehaviorPrefab;
    [SerializeField] private GameObject _wallPrefab;
    [SerializeField] private GameObject _playerPrefab;
    [SerializeField] private GameObject _endgamePrefab;
    [SerializeField] private GameObject _batPrefab;
    [SerializeField] private GameObject _owlPrefab;

    protected CellBehavior[,] cellViews;
    protected Cell[,] cells;
    protected Cell[,] previousCells;
    private Rect worldRect;

    [SerializeField] protected int width = 0;
    [SerializeField] protected int height = 0;
    private const float cellSize = 1.5f;
    [SerializeField] private int aliveToDeathConversion = 4;
    [SerializeField] private int deathToAliveConversion = 4;
    [SerializeField] private int roomSurvivalThreshold = 9;

    [SerializeField] private bool showcase = false;
    [SerializeField] private int seed = 0;
    [Range(0.0f,1.0f)][SerializeField] private double randomFillFactor = 0.5;

    private List<Region> regions_ = new List<Region>();
    [SerializeField] private int passageRadius = 1;

    public class Region : System.IComparable<Region>
    {
        private List<Vector2Int> tiles = new List<Vector2Int>();
        public List<Vector2Int> Tiles => tiles;
        private Color color_;
        public int Count => tiles.Count;

        private List<Region> connectedRooms = new List<Region>();

        private bool connectedToMainRoom = false;
        private bool isMainRoom = false;
        public Color Color
        {
            get => color_;
            set => color_ = value;
        }

        public bool ConnectedToMainRoom
        {
            get => connectedToMainRoom;
            set
            {
                bool update = !connectedToMainRoom && value;
                connectedToMainRoom = value;
                if (update)
                {
                    foreach (var region in connectedRooms)
                    {
                        if (!region.ConnectedToMainRoom)
                        {
                            region.ConnectedToMainRoom = true;
                        }
                    }
                }
                
            }
        }

        public bool IsMainRoom
        {
            get => isMainRoom;
            set => isMainRoom = value;
        }

        public bool IsConnected(Region region)
        {
            return connectedRooms.Contains(region);
        }
        public void AddNeighbor(Region region)
        {
            connectedRooms.Add(region);
        }
        public void AddTile(Vector2Int tile)
        {
            tiles.Add(tile);
        }

        public int CompareTo(Region region)
        {
            return region.Count.CompareTo(Count);
        }


    }

    protected List<Region> Regions => regions_;

    public Rect WorldRect => worldRect;

    // Start is called before the first frame update
    void Start()
    {
        Init();
        StartRoom();
    }

    protected virtual void Init()
    {
        cellViews = new CellBehavior[width, height];
        cells = new Cell[width, height];
        previousCells = new Cell[width, height];
        System.Random pseudoRandom = new System.Random();
        worldRect = new Rect()
        {
            min = new Vector2(-width / 2.0f * cellSize, -height / 2.0f * cellSize),
            max = new Vector2(width / 2.0f * cellSize, height / 2.0f * cellSize),
        };
        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                Vector3 position = new Vector3((x-width/2)*cellSize,(y-height/2)*cellSize, 0.0f);
                var cell = Instantiate(cellBehaviorPrefab, position, Quaternion.identity, transform);
                if (x == 0 || y == 0 || x == width-1 || y == height-1)
                {
                    cell.IsAlive = false;
                }
                else
                {
                    cell.IsAlive = pseudoRandom.NextDouble() < randomFillFactor;   
                }
                cellViews[x, y] = cell;
                cells[x, y] = new Cell(cell.IsAlive);
            }
        }

        if (!showcase)
        {
            for (int i = 0; i < 5; i++)
            {
                Iterate();
            }
            FloodFill();
            //Cull regions that are too small
            
            foreach (var region in regions_)
            {
                if (region.Count < roomSurvivalThreshold)
                {
                    foreach (var pos in region.Tiles)
                    {
                        cells[pos.x, pos.y].isAlive = false;
                        cellViews[pos.x, pos.y].IsAlive = false;
                    }
                }
            }

            regions_.RemoveAll(region => region.Count < roomSurvivalThreshold);
            
            regions_.Sort();
            regions_[0].IsMainRoom = true;
            regions_[0].ConnectedToMainRoom = true;
            System.DateTime start = System.DateTime.Now;
            ConnectClosestRegions();
            EnsuringConnectionToMainRoom();
            System.DateTime end = System.DateTime.Now;
            System.TimeSpan ts = (end - start);
            Debug.Log("Region connection Elapsed Time is "+ts.TotalMilliseconds+"ms");
            AddPhysicsBox();
        }
    }

    public CellBehavior GetClosestCell(Vector2 worldPos)
    {
        Vector2 position = worldPos/cellSize+new Vector2(width, height)/2.0f;
        Vector2Int pos = new Vector2Int((int) position.x, (int) position.y);
        if (pos.x < 0 || pos.x >= width || pos.y < 0 || pos.y >= height)
            return null;
        return cellViews[pos.x, pos.y];
    }
    private void AddPhysicsBox()
    {
        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                if(cells[x,y].isAlive)
                    continue;
                bool aliveNeighbor = false;
                for (int dx = -1; dx <= 1; dx++)
                {
                    for (int dy = -1; dy <= 1; dy++)
                    {
                        Vector2Int neighborPos = new Vector2Int(x + dx, y + dy);
                        if(neighborPos.x < 0 || neighborPos.x >= width || neighborPos.y < 0 || neighborPos.y >= height)
                            continue;
                        if (cells[neighborPos.x, neighborPos.y].isAlive)
                        {
                            aliveNeighbor = true;
                            break;
                        }
                        
                    }
                    if(aliveNeighbor)
                        break;
                }

                if (aliveNeighbor)
                {
                    //cellViews[x, y].gameObject.AddComponent<BoxCollider2D>();
                    
                    Vector3 position = new Vector3((x - width/2)*cellSize, (y-height/2)*cellSize, 0.0f);
                   var wall = Instantiate(_wallPrefab, position, Quaternion.identity, transform);
                   wall.layer = LayerMask.NameToLayer("Wall");
                }
            }
        }
    }

    protected virtual void ConnectClosestRegions()
    {
        foreach (var regionA in regions_)
        {
            var possibleConnectionFound = false;
            int bestDistance = 0;
            Vector2Int bestTileA = Vector2Int.zero;
            Vector2Int bestTileB = Vector2Int.zero;
            Region bestRegionB = null;
            foreach (var regionB in regions_)
            {
                if(regionA == regionB)
                    continue;

                if (regionA.IsConnected(regionB))
                {
                    possibleConnectionFound = false;
                    break;
                }

                foreach (var tileA in regionA.Tiles)
                {
                    foreach (var tileB in regionB.Tiles)
                    {
                        var delta = tileA-tileB;
                        int squareDistance = delta.x * delta.x + delta.y * delta.y;
                        if (squareDistance < bestDistance || !possibleConnectionFound)
                        {
                            possibleConnectionFound = true;
                            bestDistance = squareDistance;
                            bestTileA = tileA;
                            bestTileB = tileB;
                            bestRegionB = regionB;
                        }
                    }
                }
            }

            if (possibleConnectionFound)
            {
                CreatePassage(regionA, bestRegionB, bestTileA, bestTileB);
            }
        }
    }

    void EnsuringConnectionToMainRoom()
    {
        List<Region> regionsA = new List<Region>();
        List<Region> regionsB = new List<Region>();
        foreach (var region in regions_)
        {
            if (region.ConnectedToMainRoom)
            {
                regionsB.Add(region);
            }
            else
            {
                regionsA.Add(region);
            }
        }

        bool possibleConnectionFound = false;            
        int bestDistance = 0;
        Vector2Int bestTileA = Vector2Int.zero;
        Vector2Int bestTileB = Vector2Int.zero;
        Region bestRegionA = null;
        Region bestRegionB = null;
        foreach (var regionA in regionsA)
        {
            foreach (var regionB in regionsB)
            {
                if (regionA == regionB || regionA.IsConnected(regionB))
                    continue;
                foreach (var tileA in regionA.Tiles)
                {
                    foreach (var tileB in regionB.Tiles)
                    {
                        var delta = tileA-tileB;
                        int squareDistance = delta.x * delta.x + delta.y * delta.y;
                        if (squareDistance < bestDistance || !possibleConnectionFound)
                        {
                            possibleConnectionFound = true;
                            bestDistance = squareDistance;
                            bestTileA = tileA;
                            bestTileB = tileB;
                            bestRegionB = regionB;
                            bestRegionA = regionA;
                        }
                    }
                }
            }
        }

        if (possibleConnectionFound)
        {
            CreatePassage(bestRegionA, bestRegionB, bestTileA, bestTileB);
            EnsuringConnectionToMainRoom();
        }
    }

    void CreatePassage(Region regionA, Region regionB, Vector2Int tileA, Vector2Int tileB)
    {
        regionA.AddNeighbor(regionB);
        regionB.AddNeighbor(regionA);
        if (regionA.ConnectedToMainRoom)
        {
            regionB.ConnectedToMainRoom = true;
        }
        else if (regionB.ConnectedToMainRoom)
        {
            regionA.ConnectedToMainRoom = true;
        }

        var delta = tileB - tileA;
        Debug.DrawLine (cellViews[tileA.x,tileA.y].transform.position,
            cellViews[tileB.x,tileB.y].transform.position,
            Color.green, 100);
        
        int max = Math.Max(Math.Abs(delta.x), Math.Abs(delta.y));
        Vector2Int step = max == Math.Abs(delta.x) ? 
            Vector2Int.right * Math.Sign(delta.x) : 
            Vector2Int.up * Math.Sign(delta.y);
        float ratio = Mathf.Abs(max == Math.Abs(delta.x) ?  (float)delta.y / delta.x : (float)delta.x/delta.y);
        Vector2Int gradientStep = max != Math.Abs(delta.x) ? 
            Vector2Int.right * Math.Sign(delta.x) : 
            Vector2Int.up * Math.Sign(delta.y);
        for (int i = 0; i <= max; i++)
        {
            Vector2Int nextPos = tileA + step * i+ gradientStep*(int)(ratio*i);
            DrawCircle(nextPos, passageRadius);
        }
    }

    void DrawCircle(Vector2Int pos, int radius)
    {
        for (int x = -radius; x <= radius; x++) {
            for (int y = -radius; y <= radius; y++) {
                if (x*x + y*y <= radius*radius)
                {
                    var newPos = pos + new Vector2Int(x, y);
                    if (newPos.x < 0 || newPos.y < 0 || newPos.x >= width || newPos.y >= height)
                    {
                        continue;
                    }

                    cells[newPos.x, newPos.y].isAlive = true;
                    cellViews[newPos.x, newPos.y].IsAlive = true;
                }
            }
        }
    }

    // Update is called once per frame
    void Update()
    {
        if (showcase && Input.GetMouseButtonDown(0))
        {
            Iterate();
        }
    }

    void CopyCellIntoPrevious()
    {
        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                previousCells[x, y] = cells[x, y];
            }
        }
    }
    
    /// <summary>
    /// Make a Game of Life iteration
    /// </summary>
    void Iterate()
    {
        CopyCellIntoPrevious();
        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                int aliveNeighborCount = GetAliveNeighborCount(x, y);
                bool isAlive = previousCells[x, y].isAlive;
                //Too many alive neighbor means death
                if (isAlive && aliveNeighborCount < aliveToDeathConversion)
                {
                    cells[x, y] = new Cell(false);
                    cellViews[x, y].IsAlive = false;
                }
                //Enough living space means living
                else if(!isAlive && aliveNeighborCount > deathToAliveConversion)
                {
                    cells[x, y] = new Cell(true);
                    cellViews[x, y].IsAlive = true;
                }
            }
        }
    }
    
    void FloodFill()
    {
        System.DateTime start = System.DateTime.Now;
        bool[,] visited = new bool[width, height];

        void FillRegion(int x, int y)
        {
            Region region = new Region();
            Vector2Int pos = new Vector2Int(x, y);
            Vector2Int[] dpos = new Vector2Int[4]
            {
                new Vector2Int(-1, 0),
                new Vector2Int(0, -1),
                new Vector2Int(1, 0),
                new Vector2Int(0, 1)
            };
            Queue<Vector2Int> nextPosQueue = new Queue<Vector2Int>();
            nextPosQueue.Enqueue(pos);
            while (nextPosQueue.Count > 0)
            {
                Vector2Int currentPos = nextPosQueue.Dequeue();
                
                visited[currentPos.x, currentPos.y] = true;
                region.AddTile(currentPos);
                foreach (var delta in dpos)
                {
                    Vector2Int neighbor = currentPos + delta;
                    if (neighbor.x < 0 || neighbor.y < 0 || neighbor.x >= width || neighbor.y >= height)
                    {
                        continue;
                    }
                    if(visited[neighbor.x, neighbor.y])
                        continue;
                    if(!cells[neighbor.x, neighbor.y].isAlive)
                        continue;
                    if(nextPosQueue.Contains(neighbor))
                        continue;
                    nextPosQueue.Enqueue(neighbor);
                }
            }
            regions_.Add(region);

        }
        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                if(visited[x,y])
                    continue;
                visited[x, y] = true;
                if (cells[x, y].isAlive)
                {
                    FillRegion(x, y);
                }
            }
        }
        System.DateTime end = System.DateTime.Now;
        System.TimeSpan ts = (end - start);
        Debug.Log("Flooding Elapsed Time is "+ts.TotalMilliseconds+"ms");
    }
    int GetAliveNeighborCount(int currentX, int currentY)
    {
        int aliveNeighborCount = 0;
        for (int x = currentX - 1; x <= currentX + 1; x++)
        {
            for (int y = currentY - 1; y <= currentY + 1; y++)
            {
                if(x == currentX && y == currentY) continue;
                if(x < 0 || y < 0 || x >= width || y >= height) continue;
                aliveNeighborCount += previousCells[x, y].isAlive ? 1 : 0;
            }
        }
        return aliveNeighborCount;
    }

    void StartRoom()
    {
        // Creates a starting position for the player
        var startregion = regions_[Random.Range( 0, Regions.Count)];
        var startingtile = startregion.Tiles[Random.Range(0, startregion.Count)];
        //Creats a position for the endGame Portal
        var endregion = regions_[Random.Range( 0, Regions.Count)];
        var endtile = endregion.Tiles[Random.Range(0, endregion.Count)];
        
        Vector3 position = new Vector3((startingtile.x - width/2)*cellSize,
            (startingtile.y-height/2)*cellSize, 0.0f);
        Vector3 position2 = new Vector3((endtile.x - width/2)*cellSize,
            (endtile.y-height/2)*cellSize, 0.0f);
        
        // Instantiate the player
        var player = Instantiate(_playerPrefab, position, Quaternion.identity);
        //Instantiate the portal
        var endGame = Instantiate(_endgamePrefab, position2, Quaternion.identity);
        UnityEngine.Camera.main.GetComponent<Camera>().Player = player.transform;
        
        
        //Makes spawn enemies by regions
        foreach (var randomRegion in Regions)
            {
                var randomTile = randomRegion.Tiles[Random.Range(0, randomRegion.Count)];
                
                Vector3 randomPosition = new Vector3((randomTile.x - width / 2) * cellSize,
                    (randomTile.y - height / 2) * cellSize, 0.0f);

                if (randomRegion.Tiles.Count >= 15 && randomRegion.Tiles.Count < 60)
                {
                    Instantiate(_batPrefab, randomPosition, Quaternion.identity);
                    var batEntities = FindObjectsOfType<BatBehaviour>();
                    foreach (var steeringEntity in batEntities)
                    {
                        steeringEntity.Player = player.transform;
                    }
                }

                if (randomRegion.Tiles.Count >= 60 && randomRegion.Tiles.Count < 100)
                {
                        Instantiate(_owlPrefab, randomPosition, Quaternion.identity);
                        var owlEntities = FindObjectsOfType<OwlBehaviour>();
                        foreach (var steeringEntity in owlEntities)
                        {
                            steeringEntity.Player = player.transform;
                        }
                }

                if (randomRegion.Tiles.Count >= 100)
                {
                    Instantiate(_batPrefab, randomPosition, Quaternion.identity);
                    var batEntities = FindObjectsOfType<BatBehaviour>();
                    foreach (var steeringEntity in batEntities)
                    {
                        steeringEntity.Player = player.transform;
                    }
                    
                    Instantiate(_owlPrefab, randomPosition, Quaternion.identity);
                    var owlEntities = FindObjectsOfType<OwlBehaviour>();
                    foreach (var steeringEntity in owlEntities)
                    {
                        steeringEntity.Player = player.transform;
                    }
                }
            }

    }
}