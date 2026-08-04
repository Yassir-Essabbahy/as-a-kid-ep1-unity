using UnityEngine;

public class FloorManager : MonoBehaviour
{
    public static FloorManager Instance;

    [System.Serializable]
    public class Floor
    {
        public GameObject floorRoot; // parent object holding this floor's NPC, item, lighting, props
    }

    public Floor[] floors;
    public int currentFloorIndex = 0;

    void Awake()
    {
        Instance = this;
    }

    void Start()
    {
        ActivateFloor(currentFloorIndex);
    }

    public void ActivateFloor(int index)
    {
        for (int i = 0; i < floors.Length; i++)
        {
            floors[i].floorRoot.SetActive(i == index);
        }
        currentFloorIndex = index;
    }

    public bool HasNextFloor()
    {
        return currentFloorIndex + 1 < floors.Length;
    }
}