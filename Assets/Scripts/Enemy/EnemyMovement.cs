using UnityEngine;
using UnityEngine.AI;

public class EnemyMovement : MonoBehaviour
{
    private NavMeshAgent agent;
    [SerializeField] private GameObject particle;
    [SerializeField] private int damage;
    void Start()
    {
        agent = GetComponent<NavMeshAgent>();
        Transform target = GameObject.Find("Finish").transform;
        agent.SetDestination(target.position);
    }

    void OnTriggerEnter(Collider other)
    {
        if(other.gameObject.CompareTag("Finish"))
        {
            Vector3 finish = other.gameObject.transform.position;
            Vector3 spawnPos = new Vector3(finish.x, finish.y+1f, finish.z);
            GameObject temp = Instantiate(particle, spawnPos, Quaternion.identity);
            Destroy(temp, 3f);
            Destroy(gameObject);
            other.gameObject.GetComponent<Finish>().TakeDamage(damage);
        }
    }
}
