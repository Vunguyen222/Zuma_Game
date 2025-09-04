using NUnit.Framework;

using UnityEngine;
using UnityEngine.InputSystem.Android;
using UnityEngine.Splines;
using System.Collections.Generic;
using UnityEngine.U2D;
using System.Linq;
using UnityEngine.Rendering;

public class SplineController : MonoBehaviour
{
    private GameObject currentBall;
    private Vector3 splineStartPos;

    [HideInInspector]
    public SplineContainer spline;

    [HideInInspector]
    public Queue<GameObject>[] splinePool;

    private int process = 1;
    const int maxProcess = 10;
    void Start()
    {
        spline = GetComponent<SplineContainer>();
        GameManager.instance.ballInstances = new();
        GameManager.instance.ballRadius = 0;
        splineStartPos = new(spline.EvaluatePosition(0).x, spline.EvaluatePosition(0).y, spline.EvaluatePosition(0).z);

        splinePool = new Queue<GameObject>[GameManager.instance.ballTypes.Length];
        for (int i = 0; i < GameManager.instance.ballTypes.Length; i++)
        {
            splinePool[i] = new Queue<GameObject>();
        }

        GenBall();
    }

    // Update is called once per frame
    void Update()
    {
        float distanceSqr = Vector3.SqrMagnitude(currentBall.transform.position - splineStartPos);

        if (process != maxProcess && distanceSqr >= 4 * Mathf.Pow(GameManager.instance.ballRadius, 2))
        {
            GenBall();
            process++;
        }
    }

    void GenBall()
    {
        // random ball type
        currentBall = GameManager.instance.GenOrGetBallTypeFrom(splinePool);

        if (GameManager.instance.ballRadius == 0)
        {
            GameManager.instance.ballRadius = GetBallRadius(currentBall);
        }

        currentBall.GetComponent<Ball>().splineIndex = GameManager.instance.ballInstances.Count;
        currentBall.GetComponent<Ball>().lieOnSpline = true;

        //Debug.Log("start ball " + GameManager.instance.ballInstances.Count);

        GameManager.instance.ballInstances.Add(currentBall);

        bool hasAnimate = currentBall.GetComponent<SplineAnimate>() != null;
        if (hasAnimate)
        {
            SplineAnimate ballAnimate = currentBall.GetComponent<SplineAnimate>();
            ballAnimate.enabled = true;
            ballAnimate.NormalizedTime = 0;
            ballAnimate.Play();
        }
        else
        {
            AddAnimateAndPlay(currentBall, 0);
        }
    }

    float GetBallRadius(GameObject obj)
    {
        return obj.GetComponent<SpriteRenderer>().bounds.extents.x;
    }

    public void AddAnimateAndPlay(GameObject target, float t)
    {
        SplineAnimate ballAnimate = target.AddComponent<SplineAnimate>();
        ballAnimate.Container = spline;
        ballAnimate.ObjectUpAxis = SplineComponent.AlignAxis.ZAxis;
        ballAnimate.ObjectForwardAxis = SplineComponent.AlignAxis.XAxis;
        ballAnimate.Duration = 60;

        ballAnimate.NormalizedTime = t;
        ballAnimate.Play();
    }
}
