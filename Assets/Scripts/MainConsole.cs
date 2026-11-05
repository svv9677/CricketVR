using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;


/// <summary>
/// Main's in-world log console (HandleLog), and old delivery maths kept from 2021 that nothing
/// calls any more. Split out of Main.cs on 2026-11-05; the code is unchanged.
/// </summary>
public partial class Main
{
    private void HandleLog(string message, string stackTrace, LogType logType)
    {
        // BallDiagnostics emits several lines per physics step. Mirroring that onto the in-world
        // console buries everything else and covers the view in a headset capture. It is still in
        // the device log, which is the useful place to read it from:
        //     adb logcat -c && adb logcat -v time -s Unity:I
        if (message != null && message.StartsWith("[DIAG]"))
            return;

        string color;

        // Check if we are ignoring a certain type of message.
        if (logType == LogType.Log)
            color = "white";
        else if (logType == LogType.Warning)
            color = "yellow";
        else
            color = "red";

        string txt = "<color=" + color + ">" + System.DateTime.Now.ToLongTimeString() + " - " + message + "</color>";
        history.Add(txt);
        historyTextLength += txt.Length;

        // Enforce history and system limits
        while (history.Count > 0 && (history.Count > HISTORY_MAX_LINES || historyTextLength > HISTORY_TEXT_MAX_LENGTH))
        {
            // Remove oldest entry.
            historyTextLength -= history[0].Length;
            history.RemoveAt(0);
        }

        // Harden the history text length in case the history text length "drifts" from the history contents...
        historyTextLength = history.Count > 0 ? Mathf.Max(0, historyTextLength) : 0;

        onConsoleTextChange();
    }

    public void onConsoleTextChange()
    {
        string viewText = string.Empty;

        int start = history.Count - MAX_VIEWABLE_LINES;
        if (start < 0)
            start = 0;

        for (int i = start; i < history.Count; i++)
        {
            viewText += history[i];

            if (i < history.Count - 1)
            {
                viewText += "\n";
            }
        }

        consoleText.text = viewText;
    }

    //float GetYVel(Vector3 ballPos, float length, float x)
    //{
    //    float h = ballPos.y - 0.2f;
    //    float g = Physics.gravity.magnitude;
    //    float d = length - ballPos.x;
    //    float y = ((2 * x * x * h) - (d * d * g)) / (2 * d * x);
    //    print(y + " || " + -y);
    //    return -y;
    //}

    
    float GetYVel(Vector3 startPos, float startVelX, float length)
    {
        //Main inst = Main.Instance;
        float height = 0.1f;
        float time = (length - startPos.x) / startVelX;

        // Guard: if the ball is released from at/behind the target pitching point, `time` goes
        // zero or negative and the expression below flips sign, returning a huge POSITIVE vy -
        // the ball is fired vertically at ~40 m/s instead of being bowled. That is exactly what
        // happens when the ball is not sitting in the bowler's hand at release. Clamp to a
        // plausible flight time so a bad release position degrades to a sane delivery instead of
        // a rocket.
        const float minFlightTime = 0.15f;
        if (!(time > minFlightTime))
        {
            Debug.LogWarning($"[GetYVel] implausible flight time {time:F3}s from startPos {startPos / 3.28f} " +
                             $"to length {length / 3.28f} - the ball is probably not in the bowler's hand. " +
                             $"Clamping to {minFlightTime}s.");
            time = minFlightTime;
        }

        float startVelY = (16f * time) + (height - startPos.y / time);
        //// Air Resistance Formula
        //var p = 0.25f; // 1.225f;
        //var cd = 0.25f; // 0.47f;
        //var a = Mathf.PI * 0.0575f * 0.0575f;
        //var v = new Vector3(currentBowlingConfig.speedX, startVelY, currentBowlingConfig.speedZ).magnitude;
        //var direction = -new Vector3(currentBowlingConfig.speedX, startVelY, currentBowlingConfig.speedZ).normalized;
        //var forceAmount = (p * v * v * cd * a) / 2;

        //Vector3 right = Vector3.zero;
        //bool inSwing = UnityEngine.Random.Range(0f, 1f) > 0.5f;
        //if (inst.currentBowlingConfig.swingType == eSwingType.InSwing || inst.currentBowlingConfig.swingType == eSwingType.LegSpin ||
        //    (inst.currentBowlingConfig.swingType == eSwingType.Random && inSwing))
        //    right = Vector3.Cross(direction, Vector3.up).normalized; // direction is negative already
        //if (inst.currentBowlingConfig.swingType == eSwingType.OutSwing || inst.currentBowlingConfig.swingType == eSwingType.OffSpin ||
        //    (inst.currentBowlingConfig.swingType == eSwingType.Random && !inSwing))
        //    right = Vector3.Cross(-direction, Vector3.up).normalized;

        //forceAmount *= 10f;
        //right.x = 0f;
        //right.y = 0f;

        //float finalZ = GetFinalZ(theBall.transform.position.z, currentBowlingConfig.speedZ * Time.deltaTime / theBallRigidBody.mass, ((direction.z * forceAmount / 10f) + (right * forceAmount * inst.currentBowlingConfig.swing).z) * Time.deltaTime / theBallRigidBody.mass, time / Time.deltaTime);
        //print(finalZ);
        return startVelY;
    }

    float GetFinalZ(float startZ, float startVelZ, float velChange, float frames)
    {
        //print(startZ);
        //print(startVelZ);
        // startZ    :  meters
        // startVelZ :  meters/frame
        // velChange :  meters/frame
        // frames    :  frame
        //print("%%% " + frames + " %%% " + Factorial(frames));
        return startZ + ((startVelZ + velChange) * frames);
    }

    float Factorial(float n)
    {
        if (n <= 1)
        {
            return 1;
        }

        return n + Factorial(n - 1);
    }

    //float GetYVel(Vector3 ballStartingPos, float x, float length)
    //{
    //    float h = ballStartingPos.y - 0.2f;
    //    float g = Physics.gravity.magnitude;
    //    float d = length - ballStartingPos.x;
    //    float y = ((2 * x * x * h) - (d * d * g)) / (2 * d * x);
    //    return -y;
    //}
}
