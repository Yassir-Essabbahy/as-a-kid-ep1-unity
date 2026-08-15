using UnityEngine;

public class FloorManager : MonoBehaviour
{
    public static FloorManager Instance;

    [System.Serializable]
    public class Floor
    {
        [Tooltip("Only the environment for this floor.")]
        public GameObject floorRoot;
    }

    [Header("Floors")]
    public Floor[] floors;

    [Header("Current Floor")]
    public int currentFloorIndex = 0;


    private void Awake()
    {
        Instance = this;
    }


    private void Start()
    {
        ActivateFloor(currentFloorIndex);
    }


    public void ActivateFloor(int index)
    {
        if (floors == null || floors.Length == 0)
        {
            Debug.LogWarning(
                "FloorManager: No floors assigned."
            );

            return;
        }


        if (index < 0 || index >= floors.Length)
        {
            Debug.LogWarning(
                "FloorManager: Invalid floor index " + index
            );

            return;
        }


        for (int i = 0; i < floors.Length; i++)
        {
            if (floors[i] == null ||
                floors[i].floorRoot == null)
            {
                continue;
            }

            floors[i].floorRoot.SetActive(
                i == index
            );
        }


        currentFloorIndex = index;
    }


    public bool HasNextFloor()
    {
        return floors != null &&
               currentFloorIndex + 1 < floors.Length;
    }


    public int GetNextFloorIndex()
    {
        if (!HasNextFloor())
            return currentFloorIndex;

        return currentFloorIndex + 1;
    }
}