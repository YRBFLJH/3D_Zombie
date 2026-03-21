using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class BulletAmoutInstance : MonoBehaviour
{
    public static BulletAmoutInstance instance;

    public GameObject All;
    public TMP_Text currentBullet;
    public TMP_Text allBullet;

    void Awake()
    {
        if(instance == null)
            instance = this;
        else
            Destroy(gameObject);
    }

    public void UpdateBulletAmount(int current, int all)
    {
        currentBullet.text = current.ToString();
        allBullet.text = all.ToString();
    }
}
