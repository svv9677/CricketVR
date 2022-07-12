using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class AnimatedFielderManagement : MonoBehaviour
{

    // Dictionary for the intercept times for each fielder
    [NonSerialized]
    public Dictionary<float, AnimatedFielder> fielders;
    [NonSerialized]
    public List<float> interceptTimes;


    // Start is called before the first frame update
    void Start()
    {
        fielders = new Dictionary<float, AnimatedFielder>();
        interceptTimes = new List<float>();
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    private void LateUpdate()
    {

        if (fielders.Count >= 9)
        {
            // Get the max and add to another list, repeat until sorted.
            interceptTimes.Sort();
            fielders[interceptTimes[0]].shouldFieldBall = true;
            fielders[interceptTimes[1]].shouldFieldBall = true;
            fielders[interceptTimes[2]].shouldFieldBall = true;
            fielders[interceptTimes[3]].shouldFieldBall = false;
            fielders[interceptTimes[4]].shouldFieldBall = false;
            fielders[interceptTimes[5]].shouldFieldBall = false;
            fielders[interceptTimes[6]].shouldFieldBall = false;
            fielders[interceptTimes[7]].shouldFieldBall = false;
            fielders[interceptTimes[8]].shouldFieldBall = false;
            fielders.Clear();
            interceptTimes.Clear();
        }
    }
}
