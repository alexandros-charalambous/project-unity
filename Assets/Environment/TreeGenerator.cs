using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class TreeGenerator : MonoBehaviour
{
    GameObject tree;
    GameObject[] treesArray;

    List<GameObject> treesList = new List<GameObject>();

    public TreeGenerator()
    {
        tree = GameObject.Find("Tree_02");
    }

    public void CreateTrees(Vector3[] meshPosition)
    {
        for (int i = 0; i <= meshPosition.Length; i++)
        {
            treesList.Add(Instantiate(tree));
            treesArray = treesList.ToArray();
            treesArray[i].transform.position = meshPosition[i];
            treesArray[i].transform.parent = transform;
        }
    }
}
