using UnityEngine;

public class FoodData
{
    public int id;
    public Vector2 position;

    public float amount;      // 当前量
    public float baseAmount;  // 初始量
    public float maxAmount;   // 最大量
    public float growthRate;  // 成长速度

    public bool consumed = false;

    // ⭐ 用构造函数统一初始化（以后由系统传参）
    public FoodData(int id, Vector2 position, float baseAmount, float maxAmount, float growthRate)
    {
        this.id = id;
        this.position = position;

        this.baseAmount = baseAmount;
        this.amount = baseAmount;

        this.maxAmount = maxAmount;
        this.growthRate = growthRate;

        this.consumed = false;
    }
}