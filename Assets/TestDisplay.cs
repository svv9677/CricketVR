using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class TestDisplay : MonoBehaviour
{
    public static TestDisplay Instance;

    private TMP_Text myText;

    [SerializeField]
    private GameObject arrowPrefab;

    public List<GameObject> arrows;

    // Start is called before the first frame update
    void Start()
    {
        if (TestDisplay.Instance == null)
            TestDisplay.Instance = this;

        myText = GetComponent<TMP_Text>();
    }

    public void setText(string text)
    {
        myText.text = text;
    }

    public void addText(string text, bool newLine)
    {
        if (newLine)
            myText.text += '\n';
        myText.text += text;
    }

    public void addArrow(Vector3 from, Vector3 to, Color color)
    {
        // Below line is only applicable to bat testing
        to += from;
        GameObject myArrow = Instantiate(arrowPrefab);
        myArrow.transform.position = from;
        myArrow.transform.localScale = new Vector3(1f, 1f, Vector3.Distance(from, to));
        myArrow.transform.LookAt(to);
        MeshRenderer[] meshRenderers = myArrow.GetComponentsInChildren<MeshRenderer>();
        foreach (MeshRenderer meshRenderer in meshRenderers)
        {
            meshRenderer.material.color = color;
        }

        arrows.Add(myArrow);
    }
}
