using UnityEngine;

public class ExplodeEndEvent : MonoBehaviour
{
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    public bool isActive = true;
    void HiddenObject()
    {
        gameObject.SetActive(false);
        isActive = false;
        returnToExplosionPool();
    }

    void returnToExplosionPool()
    {
        GameManager.instance.returnExplosionEffect(gameObject);
    }
}
