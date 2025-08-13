
using UnityEngine;

public class UnhudoController : MonoBehaviour
{
 [Header("Referências")]
    public Transform player;
    public Transform shootPoint;
    public GameObject bulletPrefab;

    [Header("Alcance")]
    [Tooltip("Começa a perseguir quando o jogador estiver nesse raio")]
    public float detectionRange = 12f;
    [Tooltip("Para e atira quando estiver dentro desse raio")]
    public float stopDistance = 4f;

    [Header("Movimento / Ataque")]
    public float moveSpeed = 3f;
    public float fireRate = 1f;
    public float bulletSpeed = 10f;

    Rigidbody2D rb;
    float nextFireTime = 0f;
    bool isFacingRight = true;

    void Awake() => rb = GetComponent<Rigidbody2D>();

    void FixedUpdate()
    {
        if (player == null) return;
        float dx = player.position.x - transform.position.x;
        float absDx = Mathf.Abs(dx);

        if (absDx <= detectionRange)
        {
            if (absDx > stopDistance)
            {
                // Persegue somente até o stopDistance
                rb.velocity = new Vector2(Mathf.Sign(dx) * moveSpeed, rb.velocity.y);
            }
            else
            {
                // Estaciona antes de colidir com o player
                rb.velocity = new Vector2(0, rb.velocity.y);
            }

            if ((dx > 0f) != isFacingRight)
                Flip();

            if (absDx <= stopDistance && Time.time >= nextFireTime)
                Shoot();
        }
        else
        {
            // Fica parado fora do alcance
            rb.velocity = new Vector2(0, rb.velocity.y);
        }
    }

    void Flip()
    {
        isFacingRight = !isFacingRight;
        Vector3 s = transform.localScale;
        s.x *= -1;
        transform.localScale = s;
    }

    void Shoot()
    {
        nextFireTime = Time.time + fireRate;
        GameObject b = Instantiate(bulletPrefab, shootPoint.position, Quaternion.identity);
        Rigidbody2D r = b.GetComponent<Rigidbody2D>();
        if (r != null)
            r.velocity = (isFacingRight ? Vector2.right : Vector2.left) * bulletSpeed;
    }
}