using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class Controller : MonoBehaviour
{
    //GameObjects
    public GameObject board;
    public GameObject[] cops = new GameObject[2];
    public GameObject robber;
    public Text rounds;
    public Text finalMessage;
    public Button playAgainButton;

    //Otras variables
    Tile[] tiles = new Tile[Constants.NumTiles];
    private int roundCount = 0;
    private int state;
    private int clickedTile = -1;
    private int clickedCop = 0;
                    
    void Start()
    {        
        InitTiles();
        InitAdjacencyLists();
        state = Constants.Init;
    }
        
    //Rellenamos el array de casillas y posicionamos las fichas
    void InitTiles()
    {
        for (int fil = 0; fil < Constants.TilesPerRow; fil++)
        {
            GameObject rowchild = board.transform.GetChild(fil).gameObject;            

            for (int col = 0; col < Constants.TilesPerRow; col++)
            {
                GameObject tilechild = rowchild.transform.GetChild(col).gameObject;         
                tiles[fil * Constants.TilesPerRow + col] = tilechild.GetComponent<Tile>();                
            }
        }
                
        cops[0].GetComponent<CopMove>().currentTile = Constants.InitialCop0;
        cops[1].GetComponent<CopMove>().currentTile = Constants.InitialCop1;
        robber.GetComponent<RobberMove>().currentTile = Constants.InitialRobber;           
    }

    public void InitAdjacencyLists()
    {
        // Adjacency matrix
        int[,] matrix = new int[Constants.NumTiles, Constants.NumTiles];

        // Initialize matriz values to 0
        // NOTE(abi): this is an unnecessary step, as integer arrays are initialized to 0 by default in C#.
        //for (int i = 0; i < Constants.NumTiles; ++i)
        //{
            //for (int j = 0; j < Constants.NumTiles; ++j)
            //{
            //    matrix[i, j] = 0;
            //}
        //}

        // Set to 1 all adjacent cells for each individual cell
        // Plausible movement directions: top, bottom, right, left (no diagonals)
        for (int i = 0; i < Constants.NumTiles; ++i)
        {
            // Top
            if (i > 7) { matrix[i, i - 8] = 1; }
            
            // Bottom
            if (i < 56) { matrix[i, i + 8] = 1; }

            // Right
            if (((i + 1) % 8) != 0) { matrix[i, i + 1] = 1; }
           
            // Left
            if (i % 8 != 0) { matrix[i, i - 1] = 1; }
        }
        
        // Fill adjacency list for each cell with the indexes of its adjacent neighbours
        for (int i = 0; i < Constants.NumTiles; ++i)
        {
            for (int j = 0; j < Constants.NumTiles; ++j)
            {
                if (matrix[i, j] == 1)
                {
                    tiles[i].adjacency.Add(j);
                }
            }
        }
    }

    // Reseteamos cada casilla: color, padre, distancia y visitada
    public void ResetTiles()
    {        
        foreach (Tile tile in tiles)
        {
            tile.Reset();
        }
    }

    public void ClickOnCop(int cop_id)
    {
        switch (state)
        {
            case Constants.Init:
            case Constants.CopSelected:                
                clickedCop = cop_id;
                clickedTile = cops[cop_id].GetComponent<CopMove>().currentTile;
                tiles[clickedTile].current = true;

                ResetTiles();
                FindSelectableTiles(true);

                state = Constants.CopSelected;                
                break;            
        }
    }

    public void ClickOnTile(int t)
    {                     
        clickedTile = t;

        switch (state)
        {            
            case Constants.CopSelected:
                if (tiles[clickedTile].selectable)
                {                  
                    cops[clickedCop].GetComponent<CopMove>().MoveToTile(tiles[clickedTile]);
                    cops[clickedCop].GetComponent<CopMove>().currentTile = tiles[clickedTile].numTile;

                    tiles[clickedTile].current = true;   
                    state = Constants.TileSelected;
                }                
                break;

            case Constants.TileSelected:
                state = Constants.Init;
                break;

            case Constants.RobberTurn:
                state = Constants.Init;
                break;
        }
    }

    public void FinishTurn()
    {
        switch (state)
        {            
            case Constants.TileSelected:
                ResetTiles();
                state = Constants.RobberTurn;
                RobberTurn();
                break;

            case Constants.RobberTurn:                
                ResetTiles();
                IncreaseRoundCount();
                if (roundCount <= Constants.MaxRounds)
                    state = Constants.Init;
                else
                    EndGame(false);
                break;
        }
    }

    public void RobberTurn()
    {
        clickedTile = robber.GetComponent<RobberMove>().currentTile;
        tiles[clickedTile].current = true;
        FindSelectableTiles(false);

        /*TODO: Cambia el código de abajo para hacer lo siguiente
        - Elegimos una casilla aleatoria entre las seleccionables que puede ir el caco
        - Movemos al caco a esa casilla
        - Actualizamos la variable currentTile del caco a la nueva casilla
        */
        robber.GetComponent<RobberMove>().MoveToTile(tiles[robber.GetComponent<RobberMove>().currentTile]);
    }

    public void EndGame(bool endCode)
    {
        finalMessage.text = endCode ? "You Win!" : "You Lose!";
        playAgainButton.interactable = true;
        state = Constants.End;
    }

    public void PlayAgain()
    {
        cops[0].GetComponent<CopMove>().Restart(tiles[Constants.InitialCop0]);
        cops[1].GetComponent<CopMove>().Restart(tiles[Constants.InitialCop1]);
        robber.GetComponent<RobberMove>().Restart(tiles[Constants.InitialRobber]);
                
        ResetTiles();

        playAgainButton.interactable = false;
        finalMessage.text = "";
        roundCount = 0;
        rounds.text = "Rounds: ";

        state = Constants.Restarting;
    }

    public void InitGame() => state = Constants.Init;     

    public void IncreaseRoundCount()
    {
        roundCount++;
        rounds.text = "Rounds: " + roundCount;
    }

    public void FindSelectableTiles(bool isCop)
    {   
        int currentTileIndex = GetCurrentTileIndex(isCop);

        // The current and picked destiny cells are painted in pink
        tiles[currentTileIndex].current = true;
        tiles[currentTileIndex].visited = true;
        
        // Tiles with other cops on them
        List<int> copTileIndices = GetCopTileIndices();

        DisableAllTiles();     

        VisitTilesBFS(currentTileIndex, copTileIndices);

        // Filter by selectable tiles (no cop and reachable within 2 moves)
        foreach (Tile t in tiles)
        {
            if (!copTileIndices.Contains(t.numTile) && t.distance <= Constants.Distance)
            {
                t.selectable = true;
            }
        }
    }

    private int GetCurrentTileIndex(bool isCop) 
    {
        int index = isCop
                        ? cops[clickedCop].GetComponent<CopMove>().currentTile
                        : robber.GetComponent<RobberMove>().currentTile;

        return index;
    }

    private List<int> GetCopTileIndices()
    {
        List<int> indices = new List<int>();
        foreach (GameObject c in cops)
        {
            indices.Add(c.GetComponent<CopMove>().currentTile);
        }

        return indices;
    }

    private void DisableAllTiles()
    {
        foreach (Tile t in tiles) 
        { 
            t.selectable = false; 
        };
    } 

    private void VisitTilesBFS(int currIndex, List<int> copIndices) 
    {
        Queue<Tile> nodes = new Queue<Tile>();

        foreach (int i in tiles[currIndex].adjacency)
        {
            tiles[i].parent = tiles[currIndex];  // Root tile
            nodes.Enqueue(tiles[i]);
        }

        while (nodes.Count > 0)
        {
            Tile curr = nodes.Dequeue();
            if (!curr.visited)
            {
                if (copIndices.Contains(curr.numTile))
                {
                    curr.distance = Constants.Distance + 1;
                    curr.visited = true;
                }
                else
                {
                    foreach (int i in curr.adjacency)
                    {
                        if (!tiles[i].visited)
                        {
                            tiles[i].parent = curr;
                            nodes.Enqueue(tiles[i]);
                        }
                    }

                    curr.visited = true;
                    curr.distance = curr.parent.distance + 1;
                }
            }
        }
    }
}
