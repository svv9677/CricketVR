using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class DebugPad : MonoBehaviour
{
    [SerializeField]
    protected TMPro.TextMeshProUGUI text;

    [HideInInspector]
    public string Text;

    // Start is called before the first frame update
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        if (text.text != Text)
            text.text = Text;
    }
}
