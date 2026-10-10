using System.Collections;
using UnityEngine;

[RequireComponent(typeof(RunnerController), typeof(Rigidbody))]
public class RunnerRespawn : MonoBehaviour
{
    [Header("死亡特效")]
    public ParticleSystem shatterPrefab;

    public float respawnDelay = 0.3f;
    public float fallDistance = 8f;

    private Rigidbody body;
    private RunnerController runner;
    private Vector3 spawnPosition;
    private Quaternion spawnRotation;
    private bool respawning;

    private void Awake()
    {
        body = GetComponent<Rigidbody>();
        runner = GetComponent<RunnerController>();
        spawnPosition = body.position;
        spawnRotation = body.rotation;
    }

    private void Update()
    {
        if (!respawning && body.position.y < spawnPosition.y - fallDistance)
            Die();
    }

    private void OnCollisionEnter(Collision collision) { CheckHazard(collision); }
    private void OnCollisionStay(Collision collision) { CheckHazard(collision); }

    private void CheckHazard(Collision collision)
    {
        if (collision.collider.GetComponentInParent<RunnerHazard>() != null)
            Die();
    }

    private void Die()
    {
        if (respawning) return;
        respawning = true;
        StartCoroutine(Respawn());
    }

    private IEnumerator Respawn()
    {
        if (runner.musicClock != null) runner.musicClock.StopRun();
        runner.enabled = false;
        body.linearVelocity = Vector3.zero;
        body.angularVelocity = Vector3.zero;
        body.isKinematic = true;

        Renderer[] renderers = runner.visual != null
            ? runner.visual.GetComponentsInChildren<Renderer>(true)
            : new Renderer[0];
        bool[] visible = new bool[renderers.Length];
        for (int i = 0; i < renderers.Length; i++)
        {
            visible[i] = renderers[i].enabled;
            renderers[i].enabled = false;
        }
        //喷射粒子效果
        if (shatterPrefab != null)
        {
            ParticleSystem effect = Instantiate(
                shatterPrefab,
                body.position,
                Quaternion.identity
            );

            effect.Play();
            Destroy(effect.gameObject, 3f);
        }
        yield return new WaitForSeconds(Mathf.Max(0f, respawnDelay));

        body.position = spawnPosition;
        body.rotation = spawnRotation;
        transform.SetPositionAndRotation(spawnPosition, spawnRotation);
        Physics.SyncTransforms();
        body.isKinematic = false;
        body.linearVelocity = Vector3.zero;
        body.angularVelocity = Vector3.zero;
        runner.ResetAfterRespawn();
        for (int i = 0; i < renderers.Length; i++)
            renderers[i].enabled = visible[i];

        foreach (CameraFollow follow in FindObjectsByType<CameraFollow>(FindObjectsSortMode.None))
            if (follow.target == transform) follow.SnapToTarget();

        if (runner.musicClock != null) runner.musicClock.RestartRun();
        runner.enabled = true;
        respawning = false;
    }
}
