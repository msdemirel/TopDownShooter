using UnityEngine;

// Upgrade'lerin oyuncuya erişmek için kullandığı referans paketi.
// Bir kez kurulur; her upgrade'in tek tek GetComponent yapmasını önler.
public class PlayerContext
{
    public readonly GameObject player;
    public readonly PlayerStats stats;
    public readonly Health health;
    public readonly PlayerWeapons weapons;
    public readonly PlayerMovement movement;
    public readonly PlayerSkills skills;
    public readonly PlayerCollector collector;
    public readonly WaveManager waves;

    // Upgrade'lerin dalga kilidi bunu okur. Dalgalar başlamadan önce (0) de 1 sayılır.
    public int CurrentWave => waves != null ? Mathf.Max(1, waves.CurrentWave) : 1;

    public PlayerContext(GameObject playerObject)
    {
        waves = Object.FindFirstObjectByType<WaveManager>();
        player = playerObject;
        stats = playerObject.GetComponent<PlayerStats>();
        health = playerObject.GetComponent<Health>();
        movement = playerObject.GetComponent<PlayerMovement>();
        weapons = playerObject.GetComponentInChildren<PlayerWeapons>();
        skills = playerObject.GetComponent<PlayerSkills>();
        collector = playerObject.GetComponent<PlayerCollector>();
    }
}
