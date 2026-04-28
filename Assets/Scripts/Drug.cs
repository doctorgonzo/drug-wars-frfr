using UnityEngine;

[CreateAssetMenu(fileName = "Drug", menuName = "Scriptable Objects/Drug")]
public class Drug : Item
{
    [Tooltip("The amount of heat generated per unit of this drug traded.")]
    [Range(1, 20)]
    public int HeatValue = 5; // This is the only field you need for heat.

}

