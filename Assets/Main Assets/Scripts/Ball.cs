using NUnit.Compatibility;
using System.Collections;
using UnityEngine;
using UnityEngine.Splines;

public class Ball : MonoBehaviour
{
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    [HideInInspector]
    public int typeIndex;

    // use for ball which lie on the spline
    [HideInInspector]
    public bool lieOnSpline = false;

    [HideInInspector]
    public int splineIndex;

    // use for ball shooted from frog
    public FrogShooter frog;

    private Coroutine monitoring;
    void Start()
    {
    }

    // Update is called once per frame
    void Update()
    {
    }

    private void OnBecameInvisible()
    {
        ReturnToPool();
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        Ball otherBall = other.GetComponent<Ball>();
        GameObject otherBallObj = other.gameObject;

        if (this.lieOnSpline && !otherBall.lieOnSpline)
        {
            GameManager.instance.HandleExterCollision(splineIndex, otherBallObj);
        }
        else if (this.lieOnSpline && otherBall.lieOnSpline
            && this.splineIndex < otherBall.splineIndex && this.IsMonitorNextBall())
        {
            GameManager.instance.HandleInterCollision(splineIndex, otherBall.splineIndex);
        }
    }

    void ReturnToPool()
    {
        if (IsOffScreen())
        {
            gameObject.SetActive(false);
            frog.ballTypePool[typeIndex].Enqueue(gameObject);
        }
    }

    bool IsOffScreen()
    {
        Vector3 viewportPos = Camera.main.WorldToViewportPoint(transform.position);
        return viewportPos.x < 0 || viewportPos.x > 1 || viewportPos.y < 0 || viewportPos.y > 1;
    }

    public void StartMonitorNextBall()
    {
        monitoring = StartCoroutine(MonitoringNextBall());
        Debug.Log("monitoring " + typeIndex + " " + splineIndex);
    }

    public void StopMonitorNextBall()
    {
        StopCoroutine(monitoring);
        Debug.Log("stop monitoring " + typeIndex + " " + splineIndex);

    }

    public bool IsMonitorNextBall()
    {
        return monitoring != null;
    }

    IEnumerator MonitoringNextBall()
    {
        while (true)
        {
            GameObject nextBall = GameManager.instance.ballInstances[splineIndex + 1];
            Ball nextBallInfo = nextBall.GetComponent<Ball>();
            float distance = (nextBall.transform.position - gameObject.transform.position).magnitude;
            //Debug.Log("next ball color " + nextBallInfo.typeIndex);


            if (distance <= 2 * GameManager.instance.ballRadius + 0.1f)
            {
                if(distance < 2 * GameManager.instance.ballRadius)
                {
                    Debug.Log("push ball");
                    float fixDistance = 2 * GameManager.instance.ballRadius - distance;
                    yield return StartCoroutine(GameManager.instance.PushBallsFrom(splineIndex, fixDistance));
                }

                Debug.Log("distance small");
                if (GameManager.instance.IsTheLastSegment(nextBallInfo.splineIndex))
                {
                    GameManager.instance.RunPrevBallsFrom(splineIndex);
                    Debug.Log(splineIndex);
                }

                /* merge segment */
                GameManager.instance.MergeSegment(splineIndex + 1);

                /* to prevent coroutine stop when game object is destroyed */
                GameManager.instance.StartCheckExplosion(gameObject, splineIndex);

                StopMonitorNextBall();
            }
            else if (nextBallInfo.typeIndex == this.typeIndex)
            {
                /* move to next ball */
                yield return StartCoroutine(GameManager.instance.PullBackTheBallsFrom(splineIndex));
            }
            yield return null;
        }
    }

}
