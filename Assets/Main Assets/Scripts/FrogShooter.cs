using System.Collections;
using System.Collections.Generic;
using UnityEditor.ShaderGraph.Internal;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
public class FrogShooter : MonoBehaviour
{
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    private Vector3 shootDirection;

    private GameObject mounthBall;
    private GameObject backBall;
    private GameObject shootBall;

    public GameObject mounthHolder;
    public GameObject backHolder;
    public GameObject blinkEye;

    private float ballSpeed;
    private float rotateSpeed;

    private float blinkingTime;

    public float recoilDistance;
    public float recoilDuration;
    public float returnDuration;

    private Vector3 originalPosition;
    private bool isRecoiling;

    public Queue<GameObject>[] ballTypePool;

    void Start()
    {
        // init object pool
        ballTypePool = new Queue<GameObject>[GameManager.instance.ballTypes.Length];
        for (int i = 0; i < GameManager.instance.ballTypes.Length; i++)
        {
            ballTypePool[i] = new Queue<GameObject>();
        }

        ballSpeed = 20f;
        rotateSpeed = 20f;

        blinkingTime = 0.4f;

        recoilDistance = 0.2f;
        recoilDuration = 0.05f;
        returnDuration = 0.1f;

        originalPosition = transform.localPosition;
        isRecoiling = false;

        GenMounthBall();
        GenBackBall();

    }

    void Update()
    {
        if (Mouse.current.leftButton.wasPressedThisFrame)
        {
            ShootBall();
        }
        else if (Mouse.current.rightButton.wasPressedThisFrame)
        {
            SwapBall();
        }
    }

    void FixedUpdate()
    {
        // rotate frog follow mouse
        Vector2 mousePos = Camera.main.ScreenToWorldPoint(Mouse.current.position.ReadValue());
        Vector2 frogPos = (Vector2)transform.position;
        shootDirection = (mousePos - frogPos).normalized;

        float angle = Mathf.Atan2(shootDirection.y, shootDirection.x) * Mathf.Rad2Deg;
        Quaternion targetRotation = Quaternion.Euler(0, 0, angle + 90);
        transform.rotation = Quaternion.Lerp(transform.rotation, targetRotation, Time.fixedDeltaTime * rotateSpeed);
    }

    void GenMounthBall()
    {
        mounthBall = GameManager.instance.GenOrGetBallTypeFrom(ballTypePool);

        // sprite mask interaction
        mounthBall.GetComponent<SpriteRenderer>().maskInteraction = SpriteMaskInteraction.VisibleOutsideMask;

        // set parent
        mounthBall.transform.SetParent(mounthHolder.transform);

        // set position and direction
        mounthBall.transform.position = mounthHolder.transform.position;
        mounthBall.transform.up = -shootDirection;
    }

    void GenBackBall()
    {
        backBall = GameManager.instance.GenOrGetBallTypeFrom(ballTypePool);
        //int randomIndex = Random.Range(0, 2);
        //backBall = Instantiate(GameManager.instance.ballTypes[randomIndex]);
        //backBall.transform.localScale = new Vector3(1.2f, 1.2f, 1f);

        backBall.GetComponent<SpriteRenderer>().maskInteraction = SpriteMaskInteraction.VisibleInsideMask;
        backBall.transform.SetParent(backHolder.transform);
        backBall.transform.position = backHolder.transform.position;
        backBall.transform.up = -shootDirection;
    }

    void SwapBall()
    {
        // swap pointer
        (backBall, mounthBall) = (mounthBall, backBall);

        // swap position
        (backBall.transform.position, mounthBall.transform.position) = (mounthBall.transform.position, backBall.transform.position);

        // swap sprite mask interaction
        mounthBall.GetComponent<SpriteRenderer>().maskInteraction = SpriteMaskInteraction.VisibleOutsideMask;
        backBall.GetComponent<SpriteRenderer>().maskInteraction = SpriteMaskInteraction.VisibleInsideMask;

        // swap parent 
        mounthBall.transform.SetParent(mounthHolder.transform);
        backBall.transform.SetParent(backHolder.transform);
    }

    void ShootBall()
    {
        shootBall = mounthBall;
        shootBall.transform.SetParent(null);
        shootBall.GetComponent<SpriteRenderer>().maskInteraction = SpriteMaskInteraction.None;

        GenMounthBall();
        SwapBall();
        Vector3 shootDirAtThisFrame = shootDirection;
        shootBall.GetComponent<Rigidbody2D>().linearVelocity = shootDirAtThisFrame * ballSpeed;
        shootBall = null;

        // activate blinking animation
        StartCoroutine(BlinkRoutine());

        // activate recoil animation
        if (!isRecoiling)
        {
            StartCoroutine(Recoil());
        }
    }

    private IEnumerator BlinkRoutine()
    {
        blinkEye.SetActive(true);
        yield return new WaitForSeconds(blinkingTime);
        blinkEye.SetActive(false);
    }

    private IEnumerator Recoil()
    {
        isRecoiling = true;

        Vector3 recoilTarget = originalPosition + transform.up * recoilDistance;

        float t = 0;
        while (t < 1)
        {
            t += Time.deltaTime / recoilDuration;
            transform.localPosition = Vector3.Lerp(originalPosition, recoilTarget, t);
            yield return null;
        }

        t = 0;
        while (t < 1)
        {
            t += Time.deltaTime / returnDuration;
            transform.localPosition = Vector3.Lerp(recoilTarget, originalPosition, t);
            yield return null;
        }

        transform.localPosition = originalPosition;
        isRecoiling = false;
    }
}
