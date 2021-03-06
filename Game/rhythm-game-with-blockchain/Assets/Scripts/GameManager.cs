﻿using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

public class GameManager : MonoBehaviour {
	static public GameManager instance;

	public int bpm;

	public GameObject enemy;
	public GameObject beatIndicator;
	public GameObject gradeHolder, weaknessHolder;
	public GameObject[] buttons;
	private Stack<GameObject> currentShownButtons;
	public List<Sprite> enemies;

	public Text firstForecastText, secondForecastText, playerStatus, enemyStatus, combo, enemyName, grade;
	public Text playerStorageRate, enemyStorageRate, weaknessText;

	public GameObject levelUpNotification, careerChangeNotification;
	public Text careerChangeText;

	public AudioSource bgmPlayer;
	public GameObject effectPlayerPrefab;
	public AudioClip[] soundEffects;

	private struct KeyPress
	{
		public float timing;
		public int key;
		public KeyPress(int key,float timing)
		{
			this.key = key;
			this.timing = timing;
		}
	}
	private KeyPress[] keyPresses;

	private float deltaTime;
	private int pressIndex;
	private readonly int[,] correctInput = { { 1, 2, 1, 2 }, { 0,0,0,0 }, { 3,3,3,3 }, { 3, 0, 3, 0 } };
	private readonly KeyCode[] keys = { KeyCode.Q, KeyCode.W, KeyCode.O, KeyCode.P };

	[HideInInspector]
	public float secondsPerHalfBeat;
	[HideInInspector]
	public float secondsPerBeat;

	[HideInInspector]
	public int playerActionIndex, enemyActionIndex, playerAttackStrength, enemyAttackStrength;

	private int halfBeatCnt;
	private int enemyGenerationCnt;
	private int bgmPlayCnt;

	private bool isLevelUpTurn = false;

	private BackgroundManager backgroundManager;

	private GenesisContractService genesisContractService;

	private bool isRequestingRandomCharacter = false;

	[HideInInspector]
	public bool gameOver = false;

	private int[] mistakeCount = new int[4];

	private void Awake()
	{
		if (instance == null) instance = this;
		else Destroy(this);

		currentShownButtons = new Stack<GameObject>();

		keyPresses = new KeyPress[4];
		deltaTime = 0;
		pressIndex = 0;

		secondsPerBeat = 60f / (float)bpm;
		secondsPerHalfBeat = secondsPerBeat / 2f;
		halfBeatCnt = 16;
		enemyGenerationCnt = 1;
		bgmPlayCnt = 2;
	}

	private void Start()
	{
		genesisContractService = GetComponent<GenesisContractService>();
		genesisContractService.RequestCharacterNo();
		backgroundManager = GetComponent<BackgroundManager>();
		backgroundManager.GenerateBackground();
		StartCoroutine(HalfBeat());

		grade.text = string.Empty;
		gradeHolder.SetActive(false);
	}

	IEnumerator HalfBeat()
	{
		int lastActionOfPlayer = -1;

		while (true)
		{
			halfBeatCnt = halfBeatCnt % 16 + 1;

			if (halfBeatCnt % 2 == 1)
			{
				GetIndicatorInvisible();
			}
			else GetIndicatorVisible();

			if (isLevelUpTurn)
			{
				beatIndicator.GetComponent<SpriteRenderer>().color = Color.yellow;
			}
			else
			{
				if (halfBeatCnt == 1)
					beatIndicator.GetComponent<SpriteRenderer>().color = Color.grey;
				if (halfBeatCnt == 9)
					beatIndicator.GetComponent<SpriteRenderer>().color = Color.green;
			}

			if (isLevelUpTurn)
			{
				if (halfBeatCnt == 2)
				{
					bgmPlayCnt = bgmPlayCnt % 2 + 1;
					if (bgmPlayCnt == 1) bgmPlayer.Play();
				}
				else if (halfBeatCnt == 16)
				{
					if (Player.instance.skillPoint == 0)
					{
						isLevelUpTurn = false;
						levelUpNotification.SetActive(false);
						Player.instance.UpdateSprite();
					}
				}
					
			}
			else if (halfBeatCnt == 1)
			{
				if(Enemy.instance == null)
				{
					if (enemyGenerationCnt == 5)
					{
						if(!isRequestingRandomCharacter)
							StartCoroutine(GenerateEnemyFromGenesisCoroutine());
					}
					else
						GenerateEnemy();
				}
				
				lastActionOfPlayer = CheckInput();
				if (pressIndex != 0)
				{
					if (lastActionOfPlayer != -1)
						ShowGrade("SUCCESS",Color.green);
					else
						ShowGrade("FAIL",Color.red);
				}
				ClearInput();
			}
			else if (halfBeatCnt == 2)
			{
				bgmPlayCnt = bgmPlayCnt % 2 + 1;
				if (bgmPlayCnt == 1) bgmPlayer.Play();

				if (Enemy.instance != null)
				{
					Player.instance.Act(lastActionOfPlayer);
					Enemy.instance.Act();
				}
				else Player.instance.Act(-2); //refresh player's state
				HandleActions();
				Player.instance.CheckLevelUp();
			}
			else if (halfBeatCnt == 3)
			{
				if (Enemy.instance == null)
				{
					weaknessHolder.SetActive(false);
					weaknessText.text = string.Empty;
				}
			}
			else if (halfBeatCnt == 4)
			{
				ClearShownButtons();
			}

			yield return new WaitForSeconds(secondsPerHalfBeat);
		}
	}

	private void ShowGrade(string text, Color color)
	{
		StartCoroutine(ShowGradeCoroutine(text, color));
	}

	private IEnumerator ShowGradeCoroutine(string text,Color color)
	{
		yield return new WaitForSeconds(secondsPerHalfBeat);
		gradeHolder.SetActive(true);
		grade.text = text;
		grade.color = color;
		yield return new WaitForSeconds(secondsPerHalfBeat * 6);
		grade.text = string.Empty;
		gradeHolder.SetActive(false);
		yield break;
	}

	private void GetIndicatorVisible()
	{
		beatIndicator.SetActive(true);
	}

	private void GetIndicatorInvisible()
	{
		beatIndicator.SetActive(false);
	}

	private IEnumerator GenerateEnemyFromGenesisCoroutine()
	{
		isRequestingRandomCharacter = true;

		yield return genesisContractService.RequestRandomCharacterCoroutine();
		while (halfBeatCnt != 1)
			yield return null;
		GameObject newEnemy = Instantiate(enemy);
		newEnemy.GetComponent<Enemy>().InitializePropertiesWithRequestedCharacterInfo(genesisContractService.requestedCharacter);
		newEnemy.GetComponent<SpriteRenderer>().sprite = enemies[(int)newEnemy.GetComponent<Enemy>().career];
		AnimationManager.instance.SetEnemy(newEnemy);

		enemyGenerationCnt = enemyGenerationCnt % 5 + 1;

		isRequestingRandomCharacter = false;
	}
	private void GenerateEnemy()
	{
		GameObject newEnemy = Instantiate(enemy);
		newEnemy.GetComponent<Enemy>().career = (Character.Career)UnityEngine.Random.Range(0, 4);
		newEnemy.GetComponent<SpriteRenderer>().sprite = enemies[(int)newEnemy.GetComponent<Enemy>().career];
		newEnemy.GetComponent<Enemy>().InitializeProperties();
		AnimationManager.instance.SetEnemy(newEnemy);

		enemyGenerationCnt = enemyGenerationCnt % 5 + 1;
	}

	private void ClearInput()
	{
		pressIndex = 0;
		for (int i = 0; i < 4; ++i)
		{
			keyPresses[i].timing = 0f;
			keyPresses[i].key = -1;
		}
		deltaTime = 0f;	
	}

	private int CheckInput()
	{
		List<int> possibleMoves = new List<int>();
		Queue<int> movesToRemove = new Queue<int>();
		for (int i = 0; i < 4; ++i) possibleMoves.Add(i);
		for (int i = 0; i < 4; ++i)
		{
			foreach(int j in possibleMoves)
				if (correctInput[j, i] != keyPresses[i].key)
					movesToRemove.Enqueue(j);
			while (movesToRemove.Count > 0)
				possibleMoves.Remove(movesToRemove.Dequeue());
		}
		if (possibleMoves.Count == 0) return -1;
		int move = possibleMoves[0];

		float error = 0f;
		float maxError = secondsPerHalfBeat * secondsPerHalfBeat * 4f;

		for (int i = 0; i < 4; ++i)
		{
			float temp = keyPresses[i].timing - secondsPerBeat * i - secondsPerHalfBeat*9;
			error += temp * temp;
		}

		if (error < maxError) return move;
		else return -1;
	}

	//to do: move this into AnimationManager
	private IEnumerator ChangeWeaknessHolderColor()
	{
		const int shakeTimes = 10;
		var deltaPosition = new Vector3(5f, 5f, 0);
		for (int i = 0; i < shakeTimes; ++i)
		{
			if (weaknessHolder == null) yield break;
			weaknessHolder.transform.position += (i % 2 == 0 ? 1 : -1) * deltaPosition;
			yield return new WaitForSeconds(secondsPerBeat * 2 / shakeTimes);
		}
		yield break;
	}

	private void HandleActions()
	{
		if (playerActionIndex == Enemy.instance.weakness)
		{
			StartCoroutine(ChangeWeaknessHolderColor());
			Player.instance.StoreStrength();
		}

		bool isPlayerHurt = false, isEnemyHurt = false;

		if (playerActionIndex == 0)//player attack
		{

			if (enemyActionIndex == 0)
			{
				//enemy defended
			}
			else
			{
				Enemy.instance.ReceiveAttack(playerAttackStrength);
				Enemy.instance.ClearStrengthStorage();
				isEnemyHurt = true;

				if (enemyActionIndex == 1 || enemyActionIndex == 2)
				{
					Player.instance.ReceiveAttack(enemyAttackStrength);
					Player.instance.FailToMove();
					isPlayerHurt = true;
				}
			}
		}
		else if (playerActionIndex == 1)
		{
			if (enemyActionIndex == 1)
			{
				Player.instance.ReceiveAttack(enemyAttackStrength);
				Player.instance.FailToMove();
				isPlayerHurt = true;
			}
			else if (enemyActionIndex == 2)
			{
				//player missed
			}
		}
		else if (playerActionIndex == 2)
		{
			if (enemyActionIndex == 2)
			{
				Player.instance.ReceiveAttack(enemyAttackStrength);
				Player.instance.FailToMove();
				isPlayerHurt = true;
			}
			else if (enemyActionIndex == 1)
			{
				//player missed
			}
		}
		else if (playerActionIndex == 3 || playerActionIndex == -1)
		{
			if (enemyActionIndex == 1 || enemyActionIndex == 2)
			{
				Player.instance.ReceiveAttack(enemyAttackStrength);
				Player.instance.FailToMove();
				isPlayerHurt = true;
			}
		}

		if (enemyActionIndex == 0 && playerActionIndex != 3)
			mistakeCount[3] += 1;
		if (enemyActionIndex == 1 && playerActionIndex != 2)
			mistakeCount[2] += 1;
		if (enemyActionIndex == 2 && playerActionIndex != 1)
			mistakeCount[1] += 1;
		if (enemyActionIndex == 3 && playerActionIndex != 0)
			mistakeCount[0] += 1;

		if (playerActionIndex == 0)
			AnimationManager.instance.ScreenSplash(2);
		if (isPlayerHurt)
			AnimationManager.instance.HurtPlayer();
		if (enemyActionIndex == 1 || enemyActionIndex == 2)
			AnimationManager.instance.ScreenSplash(enemyActionIndex - 1);
		else if (enemyActionIndex == 0)
			AnimationManager.instance.LetEnemyDefend();
		if (isEnemyHurt)
			AnimationManager.instance.HurtEnemy();

		enemyActionIndex = -1;
	}

	private void UpdateUI()
	{
		if (Enemy.instance != null)
		{
			firstForecastText.text = Enemy.instance.GetForecast(0);
			secondForecastText.text = Enemy.instance.GetForecast(1);

			enemyStatus.text = "Enemy\n"
			+ Enemy.instance.currentHealth + "\n"
			+ Enemy.instance.strength + "\n"
			+ Enemy.instance.armour + "\n"
			+ Enemy.instance.luck + "\n";

			if (Enemy.instance.enemyName != string.Empty)
			{
				enemyName.text = Enemy.instance.enemyName;
				if (Enemy.instance.weakness != null)
				{
					string[] weaknessToString = { "Attack", "Dash L", "Dash R", "Store" };
					weaknessText.text = weaknessToString[(int)Enemy.instance.weakness];
					weaknessHolder.SetActive(true);
				}
			}
			else enemyName.text = enemyGenerationCnt.ToString();

			if (Mathf.Abs(Enemy.instance.storageFactor - 1f) > Mathf.Epsilon)
				enemyStorageRate.text = "+" + (int)(Enemy.instance.storageFactor * 100 - 100) + "%";
			else
				enemyStorageRate.text = string.Empty;
		}
		else
		{
			firstForecastText.text = secondForecastText.text = "";

			enemyName.text = "" + enemyGenerationCnt;
		}

		playerStatus.text = "You\n"
			+ Player.instance.currentHealth + "\n"
			+ Player.instance.strength + "\n"
			+ Player.instance.armour + "\n"
			+ Player.instance.luck + "\n";

		combo.text = "COMBO: +" + Player.instance.combo+@"0%";
		if (Mathf.Abs(Player.instance.storageFactor - 1f) > Mathf.Epsilon)
			playerStorageRate.text = "+" + (int)(Player.instance.storageFactor * 100 - 100) + "%";
		else
			playerStorageRate.text = string.Empty;
	}

	private void ClearShownButtons()
	{
		while (currentShownButtons.Count > 0)
			Destroy(currentShownButtons.Pop());
	}

	private void Update()
	{
		UpdateUI();

		if (isLevelUpTurn&&Player.instance.skillPoint>=5)
		{
			for (int i = 0; i < 4; ++i)
			{
				if (Input.GetKeyDown(keys[i]))
				{
					var effectPlayer = Instantiate(effectPlayerPrefab);
					Destroy(effectPlayer, 2f);
					var effectPlayerAudioSource = effectPlayer.GetComponent<AudioSource>();
					effectPlayerAudioSource.clip = soundEffects[i];
					effectPlayerAudioSource.Play();

					Player.instance.skillPoint -= 5;
					switch (i)
					{
						case 0: Player.instance.health += 5;Player.instance.currentHealth = Player.instance.health; break;
						case 1: Player.instance.strength += 5;break;
						case 2: Player.instance.armour += 5;break;
						case 3: Player.instance.luck += 5;break;
					}
					break;
				}
			}
		}

		deltaTime += Time.deltaTime;

		if (!isLevelUpTurn && pressIndex<4 && halfBeatCnt>8)
		{
			for(int i = 0; i < 4; ++i)
			{
				if (Input.GetKeyDown(keys[i]))
				{
					var effectPlayer = Instantiate(effectPlayerPrefab);
					Destroy(effectPlayer, 2f);
					var effectPlayerAudioSource = effectPlayer.GetComponent<AudioSource>();
					effectPlayerAudioSource.clip = soundEffects[i];
					effectPlayerAudioSource.Play();

					currentShownButtons.Push(Instantiate(buttons[i]));
					currentShownButtons.Peek().transform.position = buttons[pressIndex].transform.position;

					keyPresses[pressIndex++] = new KeyPress(i, deltaTime);

					break;
				}
			}
		}
	}

	public void ShowCareerChange(string oldCareer,string newCareer)
	{
		StartCoroutine(ShowCareerChangeCoroutine(oldCareer, newCareer));
	}

	private IEnumerator ShowCareerChangeCoroutine(string oldCareer,string newCareer)
	{
		careerChangeNotification.SetActive(true);
		careerChangeText.text = "Career changed!\n" + oldCareer + " -> " + newCareer;
		yield return new WaitForSeconds(2f);
		careerChangeNotification.SetActive(false);
	}

	public void EnterLevelUpTurn()
	{
		levelUpNotification.SetActive(true);
		isLevelUpTurn = true;
	}

	public int GetWeakness()
	{
		var index = 0;
		for(int i = 0; i < 4; ++i)
			if (mistakeCount[i] > mistakeCount[index])
				index = i;
		return index;
	}

	public void GameOver()
	{
		gameOver = true;
		SceneManager.LoadScene("End");
	}
}
