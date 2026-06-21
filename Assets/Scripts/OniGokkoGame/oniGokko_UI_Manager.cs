using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;

public class oniGokko_UI_Manager : MonoBehaviour
{
    public TextMeshProUGUI hit_text;
    // Start is called before the first frame update
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        hit_text.text = "当たった回数 : " + oniGokko_GameManager.instance.hitCount;
    }
}
