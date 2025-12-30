using UnityEngine;

[System.Serializable] // inspector 에서 보여주기 위함
public class ConnectionColorPair
{
    public bool isEnable = true;
    public Connection connection;
    public Color color;
}