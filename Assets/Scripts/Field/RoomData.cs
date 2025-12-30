using UnityEngine;

[CreateAssetMenu(fileName = "RoomData", menuName = "Map/RoomData")]
public class RoomData : ScriptableObject
{
    public string roomID;
    public GameObject roomPrefab;
    public RoomData north, south, east, west;
    public Vector2 roomCoord; 
}