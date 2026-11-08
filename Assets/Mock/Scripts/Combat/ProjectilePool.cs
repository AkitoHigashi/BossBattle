using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Pool;
using Object = UnityEngine.Object;

/// <summary>
/// 飛び道具（SkillProjectile）を使い回すためのプール。Prefab ごとに1つの ObjectPool を持つ。
/// </summary>
/// <remarks>
/// 連続突きは4秒間に10発前後撃つため、毎回 Instantiate / Destroy すると GC やスパイクの原因になる。
/// 使い終わった弾は破棄せず非表示にして取っておき、次の発射で再利用する。
///
/// static な共有プールにせず、キャラクターごと（SkillContext）に持たせている。
/// このプロジェクトは Play 開始時の Domain Reload をオフにしているため、static だと
/// 前回の Play で破棄された弾への参照が残り、次の Play で MissingReferenceException になる。
/// キャラクターと寿命を揃えれば、キャラクターの破棄時にまとめて片付けられる。
/// </remarks>
public sealed class ProjectilePool : IDisposable
{
    // 一度に飛んでいる弾の目安。足りなければ自動で増える。
    private const int DEFAULT_CAPACITY = 8;
    // 返却時にこれ以上たまっていたら、余りは破棄してメモリを抑える。
    private const int MAX_SIZE = 32;

    private readonly Dictionary<SkillProjectile, ObjectPool<SkillProjectile>> _pools = new();
    // 弾をまとめる親。Hierarchy が散らからないようにするためのもの。
    // 原点から動かさないので、子の弾のワールド座標には影響しない（キャラクターの子にすると一緒に動いてしまう）。
    private Transform _container;
    private bool _disposed;

    /// <summary>
    /// 弾を1つ取り出し、指定位置に置いて有効化する。足りなければ新しく生成する。
    /// </summary>
    public SkillProjectile Get(SkillProjectile prefab, Vector3 position, Quaternion rotation)
    {
        if (prefab == null)
            throw new ArgumentNullException(nameof(prefab));
        if (_disposed)
            throw new ObjectDisposedException(nameof(ProjectilePool));

        if (!_pools.TryGetValue(prefab, out ObjectPool<SkillProjectile> pool))
        {
            pool = CreatePool(prefab);
            _pools.Add(prefab, pool);
        }

        SkillProjectile projectile = pool.Get();
        // 有効化より前に位置を合わせると、前回消えた位置に一瞬表示されることもない。
        projectile.transform.SetPositionAndRotation(position, rotation);
        projectile.gameObject.SetActive(true);
        return projectile;
    }

    private ObjectPool<SkillProjectile> CreatePool(SkillProjectile prefab)
    {
        ObjectPool<SkillProjectile> pool = null;
        pool = new ObjectPool<SkillProjectile>(
            createFunc: () =>
            {
                SkillProjectile instance = Object.Instantiate(prefab, GetContainer());
                instance.gameObject.SetActive(false);
                // 弾は自分が飛び終わったタイミングを知っているので、返却方法を渡しておく。
                // 返却先の pool をここで捕まえることで、弾側はどのプールの所属かを知らなくてよい。
                instance.SetReleaseAction(released => Release(pool, released));
                return instance;
            },
            // 取り出し時の有効化は Get で位置を合わせた後に行うため、ここでは何もしない。
            actionOnGet: null,
            actionOnRelease: projectile => projectile.gameObject.SetActive(false),
            actionOnDestroy: projectile =>
            {
                if (projectile != null)
                    Object.Destroy(projectile.gameObject);
            },
            // 同じ弾を二重に返したら例外にする（エディタでのみ確認される）。
            collectionCheck: true,
            defaultCapacity: DEFAULT_CAPACITY,
            maxSize: MAX_SIZE);
        return pool;
    }

    private void Release(ObjectPool<SkillProjectile> pool, SkillProjectile projectile)
    {
        if (projectile == null)
            return;

        // 破棄後に返却が来た場合（通常は親ごと破棄済みで起きない）は、返す先がないのでそのまま破棄する。
        if (_disposed)
        {
            Object.Destroy(projectile.gameObject);
            return;
        }

        pool.Release(projectile);
    }

    private Transform GetContainer()
    {
        if (_container == null)
            _container = new GameObject("ProjectilePool").transform;
        return _container;
    }

    /// <summary>
    /// 待機中の弾と親オブジェクトを破棄する。キャラクターの破棄時に呼ぶ。
    /// 飛んでいる途中の弾も親オブジェクトの子なので、一緒に破棄される。
    /// </summary>
    public void Dispose()
    {
        if (_disposed)
            return;
        _disposed = true;

        foreach (ObjectPool<SkillProjectile> pool in _pools.Values)
            pool.Clear();
        _pools.Clear();

        if (_container != null)
            Object.Destroy(_container.gameObject);
        _container = null;
    }
}
