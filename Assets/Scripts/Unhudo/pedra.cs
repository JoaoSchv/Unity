using UnityEngine;
public class Bullet2D : MonoBehaviour
{
    public float lifeTime = 5f;
    public int damage = 1;

    void Awake()
    {
        Destroy(gameObject, lifeTime);
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        // Exemplo: se colidir com o jogador, aplico dano
        if (other.CompareTag("Player"))
        {
            // other.GetComponent<PlayerHealth>()?.TakeDamage(damage);
        }

        // Destrói o projétil em qualquer colisão — Player, muro, chão, inimigo, etc.
        Destroy(gameObject);
    }
}
