using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ClothController : MonoBehaviour
{
    [SerializeField] GameObject _cloth;

    public void SetClothObjectActive(bool active)
    {
        _cloth.SetActive(active);
    }
}
