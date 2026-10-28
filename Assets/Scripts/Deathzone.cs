using UnityEngine;
using UnityEngine.SceneManagement;

[RequireComponent(typeof(Collider2D))]
public sealed class Deathzone : MonoBehaviour
{
    [SerializeField] private string playerObjectName = "Player";

    private bool isReloading;

    private void Reset()
    {
        gameObject.tag = "Deathzone";
        GetComponent<Collider2D>().isTrigger = true;
    }

    private void Awake()
    {
        GetComponent<Collider2D>().isTrigger = true;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        TryReload(other.gameObject);
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        TryReload(collision.gameObject);
    }

    private void TryReload(GameObject other)
    {
        if (isReloading || !IsPlayer(other))
        {
            return;
        }

        isReloading = true;
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    private bool IsPlayer(GameObject other)
    {
        return other.GetComponent<PlayerMove>() != null || other.name == playerObjectName;
    }
}
