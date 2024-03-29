using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class GridManagerOld: MonoBehaviour {
	public GameObject Hex;
	public Tile selectedTile = null;
	public TileBehaviour originTileTB = null;
	public TileBehaviour destTileTB = null;
	public static GridManager instance = null;
	public GameObject Ground;
	public GameObject Line;

	public Dictionary<Point, TileBehaviour> Board;
	public int gridWidthInHexes = 10;
	public int gridHeightInHexes = 10;
	public List <Vector3> terrainPlacements;
	public List <GameObject> terrainTypes;
	public List<GameObject> path;


	private Vector3 initPos;
	private float hexWidth;
	private float hexHeight;
	private float groundWidth;
	private float groundHeight;


	void Awake(){
		instance = this;
	}
	//Method to initialize Hexagon width and height
	void setSize(){
		//rederer component attached to the Hex prefab is used to get the current w
		hexWidth = Hex.GetComponent<Renderer>().bounds.size.x;
		hexHeight = Hex.GetComponent<Renderer>().bounds.size.y;
		groundWidth = Ground.GetComponent<Renderer>().bounds.size.x;
		groundHeight = Ground.GetComponent<Renderer>().bounds.size.y;
	}


	//Method to calculate the position of the first hexagon tile
	//The center of the hex grid is (0,0,0)
	Vector3 calcInitPos()
	{
		Vector3 calc;
		//the initial position will be in the left upper corner
		calc = new Vector3(-hexWidth * gridWidthInHexes / 2f + hexWidth / 2, 0,
			gridHeightInHexes / 2f * hexHeight - hexHeight / 2);

		return calc;
	}


	//method used to convert hex grid coordinates to game world coordinates
	public Vector3 calcWorldCoord(Vector2 gridPos)
	{
		//Every second row is offset by half of the tile width
		float offset = 0;
		if (gridPos.y % 2 != 0)
			offset = hexWidth / 2;

		float x =  initPos.x + offset + gridPos.x * hexWidth;
		//Every new line is offset in z direction by 3/4 of the hexagon height
		float z = initPos.z - gridPos.y * hexHeight * 0.75f;
		return new Vector3(x, z, z);
	}

	//Pick terrain type
	//If none selected, grass is default.

	GameObject ChooseTerrain(int x, int y){
		//Check list to see if square's location is slated to be changed
		for (int i = 0; i < terrainPlacements.Count; i++) {
			//If it is supposed to be changed, then replace with z of 
			//Given Vector3 from terrainTypes
			if(terrainPlacements[i].x == x && terrainPlacements[i].y == y){
				return terrainTypes [(int)terrainPlacements[i].z];
			}
		}
		return Hex;
	}

	Vector2 calcGridSize()
	{
		//According to the math textbook hexagon's side length is half of the height
		float sideLength = hexHeight / 2;
		//the number of whole hex sides that fit inside inside ground height
		int nrOfSides = (int)(groundHeight / sideLength);
		//I will not try to explain the following calculation because I made some assumptions, which might not be correct in all cases, to come up with the formula. So you'll have to trust me or figure it out yourselves.
		int gridHeightInHexes = (int)( nrOfSides * 2 / 3);
		//When the number of hexes is even the tip of the last hex in the offset column might stick up.
		//The number of hexes in that case is reduced.
		if (gridHeightInHexes % 2 == 0
			&& (nrOfSides + 0.5f) * sideLength > groundHeight)
			gridHeightInHexes--;
		//gridWidth in hexes is calculated by simply dividing ground width by hex width
		return new Vector2((int)(groundWidth / hexWidth), gridHeightInHexes);
	}

	//Create and position all tiles.
	void createGrid()
	{
		Vector2 gridSize = calcGridSize();
		GameObject hexGridGO = new GameObject("HexGrid");
		//board is used to store tile locations
		Board = new Dictionary<Point, TileBehaviour>();

		for (float y = 0; y < gridSize.y; y++)
		{
			float sizeX = gridSize.x;
			//if the offset row sticks up, reduce the number of hexes in a row
			if (y % 2 != 0 && (gridSize.x + 0.5) * hexWidth > groundWidth)
				sizeX--;
			for (float x = 0; x < sizeX; x++)
			{
				GameObject hex = (GameObject)Instantiate(Hex);
				Vector2 gridPos = new Vector2(x, y);
				hex.transform.position = calcWorldCoord(gridPos);
				hex.transform.parent = hexGridGO.transform;
				var tb = (TileBehaviour)hex.GetComponent("TileBehaviour");
				//y / 2 is subtracted from x because we are using straight axis coordinate system
				tb.tile = new Tile((int)x - (int)(y / 2), (int)y);
				Board.Add(tb.tile.Location, tb);
			}
		}
		//variable to indicate if all rows have the same number of hexes in them
		//this is checked by comparing width of the first hex row plus half of the hexWidth with groundWidth
		bool equalLineLengths = (gridSize.x + 0.5) * hexWidth <= groundWidth;
		//Neighboring tile coordinates of all the tiles are calculated
		foreach(TileBehaviour tb in Board.Values)
			tb.tile.FindNeighbours(Board, gridSize, equalLineLengths);
	}


	//Distance between destination tile and some other tile in the grid
	double calcDistance(Tile tile)
	{
		Tile destTile = destTileTB.tile;
		//Formula used here can be found in Chris Schetter's article
		float deltaX = Mathf.Abs(destTile.X - tile.X);
		float deltaY = Mathf.Abs(destTile.Y - tile.Y);
		int z1 = -(tile.X + tile.Y);
		int z2 = -(destTile.X + destTile.Y);
		float deltaZ = Mathf.Abs(z2 - z1);

		return Mathf.Max(deltaX, deltaY, deltaZ);
	}

	private void DrawPath(IEnumerable<Tile> path)
	{
		if (this.path == null)
			this.path = new List<GameObject>();
		//Destroy game objects which used to indicate the path
		this.path.ForEach(Destroy);
		this.path.Clear();

		//Lines game object is used to hold all the "Line" game objects indicating the path
		GameObject lines = GameObject.Find("Lines");
		if (lines == null)
			lines = new GameObject("Lines");
		foreach (Tile tile in path)
		{
			var line = (GameObject)Instantiate(Line);
			//calcWorldCoord method uses squiggly axis coordinates so we add y / 2 to convert x coordinate from straight axis coordinate system
			Vector2 gridPos = new Vector2(tile.X + tile.Y / 2, tile.Y);
			line.transform.position = calcWorldCoord(gridPos);
			this.path.Add(line);
			line.transform.parent = lines.transform;
		}
	}

	public void generateAndShowPath()
	{
		//Don't do anything if origin or destination is not defined yet
		if (originTileTB == null || destTileTB == null)
		{
			DrawPath(new List<Tile>());
			return;
		}
		//We assume that the distance between any two adjacent tiles is 1
		//If you want to have some mountains, rivers, dirt roads or something else which might slow down the player you should replace the function with something that suits better your needs
		Func<Tile, Tile, double> distance = (node1, node2) => 1;

		var path = PathFinder.FindPath(originTileTB.tile, destTileTB.tile, 
			distance, calcDistance);
		DrawPath(path);
	}



	// Use this for initialization
	void Start () {
		setSize ();
		initPos = calcInitPos();
		createGrid ();
		generateAndShowPath ();
	}
}