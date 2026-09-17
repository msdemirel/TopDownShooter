using UnityEngine;
using UnityEngine.SceneManagement;

public class Test : MonoBehaviour
{

    [SerializeField] private int health = 60;
    [SerializeField] private int maxHealth = 100;
    [SerializeField] private int stamina = 25;

    
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        
        Debug.Log(bonus);
        CanSprint();
       
        if (IsAlive())
        {
            Debug.Log("Alive");
        }
        else
        {
            Debug.Log("Game Over");
        }
        
    }
    int bonus = 10;
    // Update is called once per frame
    void Update()
    {
        
    }
    bool IsAlive()
    {
        // IF health is greater than 0 return TRUE
        return health > 0;
    }
    bool CanSprint()
    {
        Debug.Log(bonus);
        if (!IsAlive())
        {
            return false;
        }
        return stamina > 10;
    }
    int GetMissingHealth()
    {
        return maxHealth - health;
    }
    float GetHealthPercent()
    {
        return (float)health / maxHealth * 100f;
    }

}
