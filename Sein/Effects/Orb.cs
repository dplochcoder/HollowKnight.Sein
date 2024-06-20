﻿using Mono.Security.Protocol.Tls;
using Sein.Util;
using UnityEngine;
using UnityEngine.Video;

namespace Sein.Effects;

internal class OrbParticle : MonoBehaviour
{
    private static IC.EmbeddedSprite SeinParticleSprite = new("SeinParticle");

    public static OrbParticle Instantiate()
    {
        GameObject obj = new("SeinParticle");
        obj.SetActive(false);
        obj.AddComponent<SpriteRenderer>().sprite = SeinParticleSprite.Value;
        return obj.AddComponent<OrbParticle>();
    }

    private static float INIT_SCALE = 1f;
    private static float FLIGHT_DURATION = 0.65f;
    private static float NOISE = 0.3f;

    private ObjectPool<OrbParticle> pool;
    private Vector3 start;
    private Vector3 end;
    private float time = 0;

    public void Launch(ObjectPool<OrbParticle> pool, Vector3 start, Vector3 dist, float prewarm)
    {
        if (prewarm >= FLIGHT_DURATION)
        {
            pool.Return(this);
            return;
        }

        start.x += Random.Range(-NOISE, NOISE);
        start.y += Random.Range(-NOISE, NOISE);

        this.pool = pool;
        this.start = start;
        this.end = start + dist;
        this.time = prewarm;

        transform.position = start + (end - start) * prewarm;
        transform.localScale = new(INIT_SCALE, INIT_SCALE, 1);
        gameObject.SetActive(true);
    }

    private void Update()
    {
        time += Time.deltaTime;
        if (time >= FLIGHT_DURATION)
        {
            gameObject.SetActive(false);
            pool.Return(this);
            return;
        }

        float prog = time / FLIGHT_DURATION;
        float rProg = 1 - prog;
        float sqProg = 1 - rProg * rProg;
        float scale = INIT_SCALE * (1 - prog);

        transform.position = start + (end - start) * sqProg;
        transform.localScale = new(scale, scale, 1);
    }
}

internal class Orb : MonoBehaviour
{
    public static void Hook()
    {
        SceneHooks.Hook(InstantiateOrb);
    }

    private static IC.EmbeddedSprite SeinSprite = new("Sein");

    private static void InstantiateOrb(bool oriEnabled)
    {
        if (!oriEnabled) return;

        GameObject orb = new("SeinOrb");
        orb.AddComponent<Orb>();
        orb.transform.localScale = new(SCALE, SCALE, 1);

        // TODO: Animation
        var sr = orb.AddComponent<SpriteRenderer>();
        sr.sprite = SeinSprite.Value;
    }

    private static float SCALE = 0.6f;
    private static float Z = -0.01f;
    private static float ACCEL = 23.5f;
    private static float MAX_SPEED = 60f;
    private static float MAX_IDLE_VELOCITY = 5f;
    private static float Y_OFFSET = 0.4f;
    private static float Y_RANGE = 0.15f;
    private static float Y_PERIOD = 1.25f;
    private static float X_RANGE = 0.85f;
    private static float X_PERIOD = 3.15f;
    private static Vector3 TARGET_SIZE => new(X_RANGE * 2, Y_RANGE * 2, 2 * Z);
    private static float MAX_BRAKE_DISTANCE = MAX_SPEED * MAX_SPEED / (2 * ACCEL);

    private HeroController controller;
    private GameObject knight;

    protected void Awake()
    {
        controller = GOFinder.HeroController();
        knight = controller.gameObject;
    }

    private Vector3 KnightPos => knight.transform.position;

    private Vector3 TargetPos => KnightPos + new Vector3(0, Y_OFFSET, Z);

    private float xTimer = 0;
    private float yTimer = 0;
    private Vector3 prevTarget;

    private Vector3 ComputeNewTarget()
    {
        xTimer += Time.deltaTime;
        yTimer += Time.deltaTime;
        if (xTimer > X_PERIOD) xTimer -= X_PERIOD;
        if (yTimer > Y_PERIOD) yTimer -= Y_PERIOD;

        var center = TargetPos;
        var newX = center.x + X_RANGE * Mathf.Sin(2 * xTimer * Mathf.PI / X_PERIOD);
        var newY = center.y + Y_RANGE * Mathf.Cos(2 * yTimer * Mathf.PI / Y_PERIOD);
        Vector3 newTarget = new(newX, newY, Z);

        var diff = newTarget - prevTarget;
        if (diff.sqrMagnitude > MAX_IDLE_VELOCITY * MAX_IDLE_VELOCITY)
            newTarget = prevTarget + diff.normalized * MAX_IDLE_VELOCITY;

        Bounds targetBounds = new(center, TARGET_SIZE);
        if (!targetBounds.Contains(newTarget))
        {
            // Drag target.
            if (newTarget.x > targetBounds.max.x) newTarget.x = targetBounds.max.x;
            else if (newTarget.x < targetBounds.min.x) newTarget.x = targetBounds.min.x;
            if (newTarget.y > targetBounds.max.y) newTarget.y = targetBounds.max.y;
            else if (newTarget.y < targetBounds.min.y) newTarget.y = targetBounds.min.y;
        }
        return newTarget;
    }

    private Vector3 prevVelocity = Vector3.zero;

    private Vector3 ComputeTargetVelocity(Vector3 target)
    {
        var dist = target - transform.position;
        var mag = dist.magnitude;
        if (mag <= 1e-6f) return Vector3.zero;
        if (mag >= MAX_BRAKE_DISTANCE) return dist.normalized * MAX_SPEED;

        // d = v^2/2a
        // v = sqrt(2ad)
        var targetVel = Mathf.Sqrt(2 * ACCEL * mag);
        return dist.normalized * targetVel;
    }

    private static float VEL_MULTIPLIER = 0.1f;
    private static float VEL_CAP = 2.5f;
    private static float DIST_MAX = 0.1f;
    private static float TIME_MAX = 0.25f;

    private ObjectPool<OrbParticle> particlePool = new(OrbParticle.Instantiate);
    private float particleProgress = 0;

    private void Travel(Vector3 velocity, float time)
    {
        var dist = velocity * time;
        var finalPos = transform.position + dist;

        float budget = dist.magnitude / DIST_MAX + time / TIME_MAX;
        float rate = budget / time;
        var pos = transform.position;

        float elapsed = 0;
        while (true)
        {
            float rem = 1 - particleProgress;
            if (budget >= rem)
            {
                budget -= rem;
                particleProgress = 0;

                var timeDelta = rem / rate;
                elapsed += timeDelta;
                pos += timeDelta * velocity;

                var vel = velocity * VEL_MULTIPLIER;
                if (vel.magnitude > VEL_CAP) vel = vel.normalized * VEL_CAP;
                particlePool.Lease().Launch(particlePool, pos, vel, elapsed);
            }
            else
            {
                particleProgress += budget;
                break;
            }
        }

        transform.position = finalPos;
    }

    private void AccelerateTo(Vector3 target)
    {
        var targetVel = ComputeTargetVelocity(target);
        var dist = targetVel - prevVelocity;
        Vector3 newVelocity;
        if (dist.magnitude <= ACCEL * Time.deltaTime) newVelocity = targetVel;
        else newVelocity = prevVelocity + dist.normalized * ACCEL * Time.deltaTime;

        var velocity = (newVelocity + prevVelocity) / 2;
        Travel(velocity, Time.deltaTime);
        prevVelocity = newVelocity;
    }

    private static int WAIT_FRAMES = 2;
    private int waited = 0;

    private bool WaitFrames()
    {
        if (waited == WAIT_FRAMES + 1) return true;
        else if (++waited <= WAIT_FRAMES) return false;
        else
        {
            prevTarget = KnightPos;
            transform.position = TargetPos;
            return true;
        }
    }

    private void DoAccelerate()
    {
        var newTarget = ComputeNewTarget();
        AccelerateTo(newTarget);
        prevTarget = newTarget;
    }

    private static float ROTATE_TIMER = 0.15f;
    private float rotateTimer = 0;

    private void DoRotate()
    {
        rotateTimer += Time.deltaTime;
        if (rotateTimer < ROTATE_TIMER) return;

        while (rotateTimer >= ROTATE_TIMER) rotateTimer -= ROTATE_TIMER;
        transform.localRotation = Quaternion.Euler(0, 0, Random.Range(0, 360));
    }

    protected void Update()
    {
        if (!WaitFrames()) return;

        DoAccelerate();
        DoRotate();
    }
}
