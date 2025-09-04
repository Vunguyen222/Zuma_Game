using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEditor.SceneTemplate;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Splines;

public class GameManager : MonoBehaviour
{
    struct Segment
    {
        public int start, end;

        // Replace the parameterless constructor with a method to initialize the struct
        public Segment(int start, int end)
        {
            (this.start, this.end) = (start, end);
        }
    }

    struct SegmentArray
    {
        private const int MAXSEGMENTS = 20;
        private int[] segmentStartIdx;
        private int size;

        public void Init()
        {
            segmentStartIdx = new int[MAXSEGMENTS];
            size = 0;
        }

        private readonly bool IsFull()
        {
            return size == MAXSEGMENTS;
        }

        private readonly bool IsEmpty()
        {
            return size == 0;
        }

        private readonly void InsertionSort()
        {
            if (size < 2) return;

            int sortSeg = segmentStartIdx[size - 1];
            int idx = size - 2;
            while (idx >= 0)
            {
                if (segmentStartIdx[idx] < sortSeg) break;
                else
                {
                    segmentStartIdx[idx + 1] = segmentStartIdx[idx];
                }
                idx--;
            }
            segmentStartIdx[idx + 1] = sortSeg;
        }

        /**
         * @brief Add new segment start with given index
         *
         * @param[in] segIdx    Start index of new segment
         */
        public void AddSegmentStartWith(int segIdx)
        {
            if (IsFull())
            {
                Debug.Log("segment array full");
                return;
            }

            segmentStartIdx[size] = segIdx;
            size++;
            InsertionSort();
        }

        /**
         * @brief Get a segment which contain this index
         *
         * @param[in] segIdx    Index inside this segment
         */
        public Segment GetSegmentOfIdx(int segIdx)
        {
            for (int i = 1; i < size; i++)
            {
                if (segIdx < segmentStartIdx[i])
                {
                    return new Segment(segmentStartIdx[i - 1], segmentStartIdx[i] - 1);
                }
            }

            return new Segment(segmentStartIdx[size - 1], instance.ballInstances.Count - 1);
        }

        /**
         * @brief Remove a segment start with given index
         *
         * @param[in] segIdx    Start index of the segment to be deleted
         */
        public void RemoveSegmentStartWith(int segIdx)
        {
            if (IsEmpty())
            {
                Debug.Log("segment array is empty");
                return;
            }

            for (int i = 0; i < size; i++)
            {
                if (segmentStartIdx[i] == segIdx)
                {
                    for (int j = i; j < size - 1; j++)
                    {
                        segmentStartIdx[j] = segmentStartIdx[j + 1];
                    }
                }
            }

            size--;
        }

        /**
         * @brief Update all segment's start index after given segment's start index
         *
         * @param[in] segIdx    Start index of the first segment to be updated
         */
        public void UpdateSegmentIdxFrom(int segIdx, int value)
        {
            bool update = false;

            for (int i = 0; i < size; i++)
            {
                if (update)
                {
                    segmentStartIdx[i] += value;
                }
                else
                {
                    update = (segmentStartIdx[i] == segIdx);
                }
            }
        }

        public bool isLastSegment(int segIdx)
        {
            return segmentStartIdx[size - 1] == segIdx;
        }
    }

    public static GameManager instance { get; private set; }

    public GameObject[] ballTypes;
    public SplineController splineController;
    public GameObject explosionEffect;

    [HideInInspector]
    public float ballRadius;

    [HideInInspector]
    public List<GameObject> ballInstances;

    private Queue<GameObject> explosionEffectPool;
    private SegmentArray segArr;

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(this);
        }
        else
        {
            instance = this;
        }

        for (int i = 0; i < ballTypes.Length; i++)
        {
            ballTypes[i].GetComponent<Ball>().typeIndex = i;
        }
    }
    void Start()
    {
        explosionEffectPool = new Queue<GameObject>();
        segArr.Init();
        segArr.AddSegmentStartWith(0);
    }

    void Update()
    {

    }


    public GameObject getExplosionEffect()
    {
        if (explosionEffectPool.Count > 0)
        {
            return explosionEffectPool.Dequeue();
        }
        else
        {
            return Instantiate(explosionEffect);
        }
    }

    public void returnExplosionEffect(GameObject effect)
    {
        explosionEffectPool.Enqueue(effect);
    }

    public void HandleInterCollision(int firstIndex, int secondIndex)
    {
        //StartCoroutine(CheckExplode(firstIndex, ballInstances[firstIndex]));
    }

    public void HandleExterCollision(int interBallIndex, GameObject exterCollisionBall)
    {
        // stop ball continues moving
        exterCollisionBall.GetComponent<Rigidbody2D>().linearVelocity = new(0f, 0f);
        InsertBall(interBallIndex, exterCollisionBall);
    }

    void InsertBall(int index, GameObject ball)
    {
        GameObject ballInSpline = ballInstances[index];

        Vector3 rightDir = ballInSpline.transform.right;
        Vector3 dir = ball.transform.position - ballInSpline.transform.position;
        float dotProduct = Vector2.Dot(dir, rightDir);

        if (dotProduct >= 0)
        {
            // insert before 
            InsertBallToSplineBefore(ball, index);
            InsertBallIntoList(ball, index);
            StartCheckExplosion(ball, index);
        }
        else
        {
            /* insert after */

            // get segment
            Segment curSeg = segArr.GetSegmentOfIdx(index);

            if (index != curSeg.end)
            {
                InsertBall(index + 1, ball);
            }
            else
            {
                /* moving ball only if inserting to last segment */
                bool ballMoving = false;
                if (segArr.isLastSegment(curSeg.start))
                {
                    ballMoving = true;
                }

                // caculate next t
                float currentT = ballInSpline.GetComponent<SplineAnimate>().NormalizedTime;
                float nextT = FindNextPos(currentT, 2 * ballRadius, false);

                ball.GetComponent<Ball>().lieOnSpline = true;

                // insert position
                StartCoroutine(MoveBallToSpline(ball, nextT, 0.2f, ballMoving));

                // insert into list 
                ball.GetComponent<Ball>().splineIndex = index + 1;

                if (segArr.isLastSegment(curSeg.start))
                {
                    ballInstances.Add(ball);
                }
                else
                {
                    ballInstances.Insert(index + 1, ball);
                    UpdateBallsIndexFrom(index + 2);
                }

                // update monitoring ball
                Ball prevBallInfo = ballInstances[index].GetComponent<Ball>();
                Ball curBallInfo = ball.GetComponent<Ball>();

                if (prevBallInfo.IsMonitorNextBall())
                {
                    prevBallInfo.StopMonitorNextBall();
                    curBallInfo.StartMonitorNextBall();
                }

                // update segment array
                segArr.UpdateSegmentIdxFrom(curSeg.start, 1);

                StartCheckExplosion(ball, index);
            }
        }
    }

    float FindNextPos(float currentT, float distance, bool forward = true)
    {
        float step = 0.001f;
        float totalDistance = 0f;

        Vector3 currentPos = splineController.spline.EvaluatePosition(currentT);
        Vector3 nextPos;

        while (totalDistance < distance)
        {
            currentT = forward ? (currentT + step) : (currentT - step);
            nextPos = splineController.spline.EvaluatePosition(currentT);
            totalDistance += (nextPos - currentPos).magnitude;
            currentPos = nextPos;
        }

        return currentT;
    }

    void InsertBallToSplineBefore(GameObject obj, int index)
    {
        // get segment
        Segment curSeg = segArr.GetSegmentOfIdx(index);
        bool ballMoving = false;

        if (segArr.isLastSegment(curSeg.start))
        {
            ballMoving = true;
        }

        // insert position
        float currentT = ballInstances[curSeg.start].GetComponent<SplineAnimate>().NormalizedTime;
        float nextT = FindNextPos(currentT, 2 * ballRadius);

        if (index != curSeg.start)
        {
            // move first ball to next pos
            StartCoroutine(MoveBallToSpline(ballInstances[curSeg.start], nextT, 0.2f, ballMoving));

            nextT = currentT;

            // move balls before index to next pos 
            for (int i = curSeg.start + 1; i < index; i++)
            {
                currentT = ballInstances[i].GetComponent<SplineAnimate>().NormalizedTime;
                StartCoroutine(MoveBallToSpline(ballInstances[i], nextT, 0.2f, ballMoving));
                nextT = currentT;
            }
        }

        obj.GetComponent<Ball>().lieOnSpline = true;

        // update segment array
        segArr.UpdateSegmentIdxFrom(curSeg.start, 1);

        // insert ball to spline + add spline animate to this ball + insert smoothly
        StartCoroutine(MoveBallToSpline(obj, nextT, 0.2f, ballMoving));
    }

    void InsertBallIntoList(GameObject obj, int index)
    {
        obj.GetComponent<Ball>().splineIndex = index;

        ballInstances.Insert(index, obj);

        //update index for the other balls
        UpdateBallsIndexFrom(index + 1);
    }

    void InsertBallIntoPool(int index)
    {
        int typeIndex = ballInstances[index].GetComponent<Ball>().typeIndex;
        splineController.splinePool[typeIndex].Enqueue(ballInstances[index]);
    }

    void DelBallFromSplineAt(int index)
    {
        ballInstances[index].SetActive(false);
        ballInstances[index].GetComponent<Ball>().lieOnSpline = false;
        ballInstances[index].GetComponent<SplineAnimate>().enabled = false;
    }

    GameObject ExplodeBall(int index)
    {
        GameObject expEffect = getExplosionEffect();
        expEffect.transform.rotation = Quaternion.identity;

        // make explosion follow the ball
        bool hasAnimate = expEffect.GetComponent<SplineAnimate>() != null;
        float t = ballInstances[index].GetComponent<SplineAnimate>().NormalizedTime;
        if (hasAnimate)
        {
            expEffect.GetComponent<SplineAnimate>().NormalizedTime = t;
            expEffect.GetComponent<SplineAnimate>().Play();
        }
        else
        {
            splineController.AddAnimateAndPlay(expEffect, t);
        }

        expEffect.SetActive(true);
        return expEffect;
    }

    void UpdateBallsIndexFrom(int startIdx)
    {
        for (int i = startIdx; i < ballInstances.Count; i++)
        {
            ballInstances[i].GetComponent<Ball>().splineIndex = i;
        }
    }

    /*------------------------------------ COMMON FUNCTION -------------------------------------------*/

    public GameObject GenOrGetBallTypeFrom(Queue<GameObject>[] pool)
    {
        int randomIndex = Random.Range(0, GameManager.instance.ballTypes.Length);
        GameObject tempBall;

        if (pool[randomIndex].Count != 0)
        {
            tempBall = pool[randomIndex].Dequeue();
            tempBall.SetActive(true);
        }
        else
        {
            tempBall = Instantiate(GameManager.instance.ballTypes[randomIndex]);
            tempBall.transform.localScale = new Vector3(1.2f, 1.2f, 1f);
        }
        return tempBall;
    }
    public void StopPrevBallsFrom(int index)
    {
        // get segment
        Segment curSeg = segArr.GetSegmentOfIdx(index);
        for (int i = index; i >= curSeg.start; i--)
        {
            ballInstances[i].GetComponent<SplineAnimate>().Pause();
        }
    }

    public void RunPrevBallsFrom(int index)
    {
        // get segment 
        Segment curSeg = segArr.GetSegmentOfIdx(index);

        for (int i = index; i >= curSeg.start; i--)
        {
            ballInstances[i].GetComponent<SplineAnimate>().Play();
        }
    }

    public void StartCheckExplosion(GameObject ball, int index)
    {
        StartCoroutine(CheckExplode(ball, index));
    }

    public void MergeSegment(int index)
    {
        segArr.RemoveSegmentStartWith(index);
    }

    public bool IsTheLastSegment(int index)
    {
        return segArr.isLastSegment(index);
    }

    /*---------------------------------------- COROUTINE -------------------------------------------*/

    // function to move ball smoothly
    public IEnumerator MoveBallToSpline(GameObject ball, float nextT, float duration, bool isBallMoving = true)
    {
        bool hasAnimate = (ball.GetComponent<SplineAnimate>() != null);

        GameObject target = Instantiate(ball);
        target.GetComponent<Renderer>().enabled = false;

        if (hasAnimate)
        {
            ball.GetComponent<SplineAnimate>().Pause();
            target.GetComponent<SplineAnimate>().NormalizedTime = nextT;
            target.GetComponent<SplineAnimate>().Play();
        }
        else
        {
            splineController.AddAnimateAndPlay(target, nextT);
        }

        if (!isBallMoving)
        {
            target.GetComponent<SplineAnimate>().Pause();
        }

        Vector3 startPos = ball.transform.position;
        Vector3 targetPos;

        float elapsed = 0f;

        while (elapsed < duration)
        {
            targetPos = splineController.spline.EvaluatePosition(target.GetComponent<SplineAnimate>().NormalizedTime);
            float t = elapsed / duration;
            ball.transform.position = Vector3.Lerp(startPos, targetPos, t);
            elapsed += Time.deltaTime;
            yield return null;
        }

        float currentT = target.GetComponent<SplineAnimate>().NormalizedTime;
        Destroy(target);

        if (!hasAnimate)
        {
            splineController.AddAnimateAndPlay(ball, currentT);
        }
        else
        {
            ball.GetComponent<SplineAnimate>().NormalizedTime = currentT;
            ball.GetComponent<SplineAnimate>().Play();
        }

        if (!isBallMoving)
        {
            ball.GetComponent<SplineAnimate>().Pause();
        }
    }

    public IEnumerator CheckExplode(GameObject ball, int index)
    {
        // wait until the ball totally inserted into list
        yield return new WaitUntil(() => ball.GetComponent<SplineAnimate>() != null
                                    && ball.GetComponent<SplineAnimate>().IsPlaying);

        // get segment
        Segment curSeg = segArr.GetSegmentOfIdx(index);

        Debug.Log(curSeg.start + " " + curSeg.end);

        int prev = index - 1;
        int next = index + 1;
        int typeOfThisBall = ball.GetComponent<Ball>().typeIndex;

        bool isFistChain = false;
        bool isLastChain = false;

        // check forward
        while (prev >= curSeg.start
            && ballInstances[prev].GetComponent<Ball>().typeIndex == typeOfThisBall)
        {
            prev--;
        }
        if (prev < curSeg.start) isFistChain = true;

        // check after
        while (next <= curSeg.end
            && ballInstances[next].GetComponent<Ball>().typeIndex == typeOfThisBall)
        {
            next++;
        }
        if (next > curSeg.end) isLastChain = true;

        if (next - prev - 1 < 3) yield break;

        // if balls >= 3 --> del ball
        GameObject lastExplosion = null;
        for (int i = prev + 1; i < next; i++)
        {
            if (i == next - 1)
            {
                lastExplosion = ExplodeBall(i);
            }
            else
            {
                ExplodeBall(i);
            }
            InsertBallIntoPool(i);
            DelBallFromSplineAt(i);
        }

        // del balls from list
        ballInstances.RemoveRange(prev + 1, next - prev - 1);
        UpdateBallsIndexFrom(prev + 1);

        // update segment
        segArr.UpdateSegmentIdxFrom(curSeg.start, -(next - prev - 1));

        if (!isFistChain && !isLastChain)
        {
            // wait until the last explosion finish
            yield return new WaitUntil(() => lastExplosion != null && !lastExplosion.activeSelf);

            // add new segment
            segArr.AddSegmentStartWith(prev + 1);

            // stop all previous balls from prev
            StopPrevBallsFrom(prev);

            ballInstances[prev].GetComponent<Ball>().StartMonitorNextBall();
        }
    }

    public IEnumerator PullBackTheBallsFrom(int index)
    {
        float currentT, nextT;
        float step = ballRadius / 4f;
        float variance = 0.00f;

        GameObject curBall = ballInstances[index];
        GameObject nextBall = ballInstances[index + 1];
        float distance = (nextBall.transform.position - curBall.transform.position).magnitude;

        // get segment
        Segment curSeg = segArr.GetSegmentOfIdx(index);

        /* if the distance is less than step, stop and waiting for the next ball */
        if (distance - 2 * ballRadius - variance >= step)
        {
            for (int i = index; i >= curSeg.start; i--)
            {
                currentT = ballInstances[i].GetComponent<SplineAnimate>().NormalizedTime;
                nextT = FindNextPos(currentT, step, false);

                if (i == curSeg.start)
                {
                    yield return StartCoroutine(MoveBallToSpline(ballInstances[i], nextT, 0.01f, false));
                }
                else
                {
                    StartCoroutine(MoveBallToSpline(ballInstances[i], nextT, 0.01f, false));
                }
            }
        }
        //else
        //{
        //    /* Move ball to spline make ball run, need to stop ball */
        //    StopPrevBallsFrom(index);
        //}
    }

    public IEnumerator PushBallsFrom(int index, float distance)
    {
        // get segment
        Segment curSeg = segArr.GetSegmentOfIdx(index);

        float currentT, nextT;

        for (int i = index; i >= curSeg.start; i--)
        {
            currentT = ballInstances[i].GetComponent<SplineAnimate>().NormalizedTime;
            nextT = FindNextPos(currentT, distance);

            if (i == curSeg.start)
            {
                yield return StartCoroutine(MoveBallToSpline(ballInstances[i], nextT, 0.01f, false));
            }
            else
            {
                StartCoroutine(MoveBallToSpline(ballInstances[i], nextT, 0.01f, false));
            }

            //StopPrevBallsFrom(index);
        }

    }
}
