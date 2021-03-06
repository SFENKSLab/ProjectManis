﻿using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Character : MonoBehaviour {
	public int skillPoint = 0, health = 100, strength = 50, luck = 100, armour = 25, currentHealth = 100;
	public int level = 1;

	public float storageFactor;

	public enum Career
	{
		Warrier = 0, Tank = 1, Magician = 2, Thief = 3
	}

	public Career GetCareer()
	{
		if (health >= strength && health >= armour*3 && health >= luck)
			return Career.Warrier;
		else if (armour*3 >= strength && armour*3 >= luck)
			return Career.Tank;
		else if (strength >= luck)
			return Career.Magician;
		else return Career.Thief;
	}

	protected void LevelUp()
	{
		skillPoint += 30;
		health += 10;
		strength += 10;
		luck += 5;
		armour += 5;
		level += 1;
		currentHealth = health;
	}

	public void ReceiveAttack(int attakerStrength)
	{
		currentHealth -= UnityEngine.Mathf.Max(0, attakerStrength - armour);
		if (currentHealth <= 0)
			TryToDestroy();
	}

	virtual protected void TryToDestroy() { }

	public void ClearStrengthStorage()
	{
		storageFactor = 1f;
	}

	public void StoreStrength()
	{
		storageFactor += 1.25f;
	}
}
