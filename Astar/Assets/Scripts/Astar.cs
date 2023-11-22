using UnityEngine;
using System.Collections.Generic;
using Unity.Burst;
using Unity.Mathematics;
using Unity.Collections;
using System.Linq;

[BurstCompile]
public class Astar
{
    [SerializeField] private int2 gridSize;
    [ReadOnly] private const int StraightCost = 10;
    private NativeList<int2> newPath = new(Allocator.Persistent);

    [BurstCompile]
    public List<int2> FindPathToTarget(int2 startPos, int2 endPos, Cell[,] grid)
    {
        gridSize = new(grid.GetLength(0), grid.GetLength(1));
        newPath.Clear();

        if (!IsValidPositionInGrid(endPos, gridSize))
        {
            Debug.LogWarning("Not a valid position in the grid.");
            return new List<int2>();
        }

        NativeArray<Wall> walls = new(gridSize.x * gridSize.y, Allocator.TempJob);

        for (int x = 0; x < gridSize.x; x++)
        {
            for (int y = 0; y < gridSize.y; y++)
            {
                int index = CalculateFlatIndex(new(x, y), gridSize);
                walls[index] = grid[x, y].walls;
            }
        }

        FindPath(startPos, endPos, walls);
        List<int2> tempPath = new();

        foreach (int2 position in newPath)
        {
            tempPath.Add(position);
        }
        tempPath.Reverse();

        walls.Dispose();
        return tempPath;
    }

    [BurstCompile]
    private void FindPath(int2 StartPosition, int2 EndPosition, NativeArray<Wall> Walls)
    {
        NativeArray<Node> nodePath = new(gridSize.x * gridSize.y, Allocator.Temp);

        for (int x = 0; x < gridSize.x; x++)
        {
            for (int y = 0; y < gridSize.y; y++)
            {
                Node node = new()
                {
                    Position = new int2(x, y),
                    GScore = int.MaxValue,
                    IsWalkable = true,
                    PreviousNodeIndex = -1,
                    Index = CalculateFlatIndex(new int2(x, y), gridSize)
                };

                node.HScore = EstimateDistanceCost(node.Position, EndPosition);
                nodePath[node.Index] = node;
            }
        }

        //Offsets for the neighbouring nodes.
        NativeArray<int2> neighbourOffsetArray = new(8, Allocator.Temp);
        neighbourOffsetArray[0] = new int2(-1, 0);
        neighbourOffsetArray[1] = new int2(+1, 0);
        neighbourOffsetArray[2] = new int2(0, +1);
        neighbourOffsetArray[3] = new int2(0, -1);

        int endNodeIndex = CalculateFlatIndex(EndPosition, gridSize);

        Node startNode = nodePath[CalculateFlatIndex(StartPosition, gridSize)];
        startNode.GScore = 0;
        nodePath[startNode.Index] = startNode;

        NativeList<int> openList = new(Allocator.Temp);
        NativeList<int> closedList = new(Allocator.Temp);

        openList.Add(startNode.Index);

        while (openList.Length > 0)
        {
            Node currentNode = nodePath[ReturnLowestFScoreIndex(nodePath, openList)];

            if (currentNode.Index == endNodeIndex)
            {
                break;
            }

            for (int i = 0; i < openList.Length; i++)
            {
                if (currentNode.Index == openList[i])
                {
                    openList.RemoveAtSwapBack(i);
                }
            }

            closedList.Add(currentNode.Index);

            foreach (int2 offset in neighbourOffsetArray)
            {
                int2 neighbourNodePosition = currentNode.Position + offset;

                if (!IsValidPositionInGrid(neighbourNodePosition, gridSize))
                {
                    continue;
                }

                int neighBourIndex = CalculateFlatIndex(neighbourNodePosition, gridSize);
                Node neighBourNode = nodePath[neighBourIndex];

                bool wallInTheWay = false;
                NativeList<int2> wallsOffset = ReturnWallsDirection(Walls[neighBourIndex]);
                foreach (int2 wall in wallsOffset)
                {
                    if (offset.x * -1 == wall.x && offset.y * -1 == wall.y)
                    {
                        wallInTheWay = true;
                    }
                }
                NativeList<int2> wallsOffsetCurrentNode = ReturnWallsDirection(Walls[currentNode.Index]);
                foreach (int2 wall in wallsOffsetCurrentNode)
                {
                    if (offset.x == wall.x && offset.y == wall.y)
                    {
                        wallInTheWay = true;
                    }
                }

                if (closedList.Contains(neighBourNode.Index) || !neighBourNode.IsWalkable || wallInTheWay)
                {
                    continue;
                }

                int GCost = currentNode.GScore + EstimateDistanceCost(currentNode.Position, neighBourNode.Position);
                if (GCost < neighBourNode.GScore)
                {
                    neighBourNode.PreviousNodeIndex = currentNode.Index;
                    neighBourNode.GScore = GCost;
                    nodePath[neighBourNode.Index] = neighBourNode;

                    if (!openList.Contains(neighBourNode.Index))
                    {
                        openList.Add(neighBourNode.Index);
                    }
                }

                wallsOffset.Dispose();
                wallsOffsetCurrentNode.Dispose();
            }
        }

        if (endNodeIndex >= nodePath.Length || endNodeIndex < 0) { return; }

        Node endNode = nodePath[endNodeIndex];
        if (endNode.PreviousNodeIndex == -1)
        {
            Debug.Log("No Path Was Found.");
        }
        else
        {
            CalculatePath(nodePath, endNode);
        }

        openList.Dispose();
        closedList.Dispose();
        nodePath.Dispose();
        neighbourOffsetArray.Dispose();
    }


    [BurstCompile]
    private NativeList<int2> ReturnWallsDirection(Wall wall)
    {
        NativeList<int2> wallDirections = new(Allocator.Temp);

        if ((wall & Wall.RIGHT) != 0)
        {
            wallDirections.Add(new(1, 0));
        }
        if ((wall & Wall.UP) != 0)
        {
            wallDirections.Add(new(0, 1));
        }
        if ((wall & Wall.DOWN) != 0)
        {
            wallDirections.Add(new(0, -1));
        }
        if ((wall & Wall.LEFT) != 0)
        {
            wallDirections.Add(new(-1, 0));
        }

        return wallDirections;
    }

    [BurstCompile]
    private int EstimateDistanceCost(int2 position1, int2 position2)
    {
        int xDistance = math.abs(position1.x - position2.x);
        int yDistance = math.abs(position1.y - position2.y);
        int remainder = math.abs(xDistance - yDistance);
        return StraightCost * remainder;
    }

    private int CalculateFlatIndex(int2 position, int2 gridSize)
    {
        return position.x + position.y * gridSize.x;
    }

    [BurstCompile]
    private bool IsValidPositionInGrid(int2 position, int2 gridSize)
    {
        return
            position.x < gridSize.x &&
            position.y < gridSize.y &&
            position.y >= 0 &&
            position.x >= 0;
    }

    [BurstCompile]
    private void CalculatePath(NativeArray<Node> nodePath, Node endNode)
    {
        if (endNode.PreviousNodeIndex == -1)
        {
            return;
        }

        newPath.Add(endNode.Position);

        Node currentNode = endNode;
        while (currentNode.PreviousNodeIndex != -1)
        {
            Node previousNode = nodePath[currentNode.PreviousNodeIndex];
            newPath.Add(previousNode.Position);
            currentNode = previousNode;
        }
    }

    [BurstCompile]
    private int ReturnLowestFScoreIndex(NativeArray<Node> nodes, NativeList<int> openList)
    {
        Node currentNode = nodes[openList[0]];

        for (int i = 0; i < openList.Length; i++)
        {
            if (currentNode.FScore > nodes[openList[0]].FScore)
            {
                currentNode = nodes[openList[0]];
            }
        }

        return currentNode.Index;
    }

    public struct Node
    {
        public int2 Position;
        public int Index;

        public int PreviousNodeIndex;

        public bool IsWalkable;

        public int FScore
        {
            get { return GScore + HScore; }
        }
        public int GScore;
        public int HScore;
    }
}
