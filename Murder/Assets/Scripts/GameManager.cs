﻿using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class GameManager : MonoBehaviour {

    [Header("Preferences")]
    const int playerQuantity = 2;
    [SerializeField] private int charactersQuantity;

    [SerializeField] private GameObject characterPrefab, ammoPrefab;
    [SerializeField] private MapCreator mapCreator;
    [SerializeField] private List<GameObject> characters = new List<GameObject>();
    [SerializeField] private List<GameObject> ammos = new List<GameObject>();
    [SerializeField] private GameData gameData;

    private Dictionary<Vector2,GameObject> nextPositions = new Dictionary<Vector2, GameObject>();
    [SerializeField] private bool[] shallShoot;
    private Vector2[] shootDirection;

    [SerializeField] private Text gameState;

    [SerializeField] private GameObject p1Icon, p2Icon;
    [SerializeField] private Color[] colorOptions;

    [SerializeField] private AudioSource[] audioSrcs;

    private bool ended;
    private int currentRound;
    private int[] playersScore = {0,0};

	void Start () {
        mapCreator.ReadMapData(gameData.mapNumber);
		mapCreator.InitializeMap();
        StartCoroutine(GameLoop());
    }

    private void InitializeRound(){
        for(int i = characters.Count-1; i > 0; i--){
            Destroy(characters[i]);
        }
        characters = new List<GameObject>();
        InitializeCharacters();
        InitializeCharacters();
        InitializeCharacters();
        InitializeCharacters();

        shallShoot = new bool[playerQuantity];
        shootDirection = new Vector2[playerQuantity];

        SpawnAmmo();
        SpawnAmmo();
    }
    private IEnumerator GameLoop(){
        int rounds = 1;
        if(gameData.rounds == RoundQuantity.THREE){
            rounds = 3;
        } if(gameData.rounds == RoundQuantity.FIVE){
            rounds = 5;
        }
        for(currentRound = 0; currentRound < rounds; currentRound++){
            ended = false;
            InitializeRound();
            print("Round "+(currentRound+1).ToString());
            print("Max Round "+rounds);
            if(playersScore[0] > (float)rounds/2 || playersScore[1] > (float)rounds/2){
                //animacao final
                yield break;
            }
            //animacao inicio round
            while(!ended){
                nextPositions.Clear();
                AddObstaclesInDictionary();
                RandomizeCharactersMovement();
                yield return WaitForPlayerInput(0);
                yield return WaitForPlayerInput(1);
                if (isMurderTime()) {
                    ShootBullets();
                    ended = true;
                } else {
                    yield return NewPositionsHandler();
                    yield return new WaitForEndOfFrame();
                }
            }
            //animacao final rounds - com ganhador
        }
    }
    private Vector2 GetSpawnPoint(){
        var map = mapCreator.map;
        List<Vector2> list = new List<Vector2>();
        for(int x = 0; x < 10; x++){
            for(int y = 0; y < 10; y++){
                if(map[x,y] == 0){
                    list.Add(new Vector2(x,y));
                }
            }
        }
        return list[Random.Range(0,list.Count)];
    }
    private IEnumerator WaitForPlayerInput(int playerId){
        Vector2 dir = Vector2.zero;
        Character character = characters[playerId].GetComponent<Character>();
        gameState.text = "Get Player " + playerId + " input";

        while (!Input.GetKeyDown(KeyCode.Return)){
            if (Input.GetKeyDown(KeyCode.Space)) {
                if (character.hasBullet) {
                    audioSrcs[1].Play();
                    gameState.text = "Player " + playerId + ": select direction";
                    shallShoot[playerId] = true;
                    yield return GetShootDirection(playerId);
                } else {
                    //error feedback
                }
            }

            Vector2 temp_dir = new Vector2(Input.GetAxisRaw("Horizontal"),Input.GetAxisRaw("Vertical"));
            if (temp_dir != Vector2.zero) {
                gameState.text = "Player " + playerId + ": Press Start";
                dir = temp_dir;
            }
            yield return null;
        }
        audioSrcs[0].Play();
        Vector2 newPos = character.position + dir;
        AddOnDictionary(newPos,characters[playerId]);
        yield return new WaitWhile(() => Input.GetKeyDown(KeyCode.Return));
    }
    private IEnumerator GetShootDirection(int id) {
        Vector2 temp_dir = new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical"));
        while(temp_dir == Vector2.zero) {
            temp_dir = new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical"));
            if(temp_dir.x != 0 && temp_dir.y != 0) { temp_dir.x = 0; }
            yield return null;
        }
        shootDirection[id] = temp_dir;
    }
    private bool isMurderTime() {
        foreach(bool b in shallShoot) {
            if (b) return true;
        } return false;
    }
    private IEnumerator NewPositionsHandler(){
        gameState.text = "Moving";
        foreach (Vector2 v in nextPositions.Keys){
            if(nextPositions[v].GetComponent<Character>() != null){
                nextPositions[v].GetComponent<Character>().SetMovement(v);
            }
        }
        CheckAmmoCollect();
        yield return new WaitForEndOfFrame();
    }
    private void CheckAmmoCollect() {
        bool ammoColleted = false;
        GameObject collected = new GameObject();
        foreach(GameObject a in ammos) {
            Ammo ammo = a.GetComponent<Ammo>();
            foreach(GameObject b in characters) {
                Character character = b.GetComponent<Character>();
                if(ammo.position == character.position) {
                    character.SetAmmo(true);
                    collected = a;
                    ammoColleted = true;
                }
            }
        }
        if (ammoColleted) {
            ammos.Remove(collected);
            collected.GetComponent<Ammo>().Collect();
        }
    }
    private void ShootBullets() {
        for(int i = 0; i < shallShoot.Length; i++) {
            if (shallShoot[i]) {
                CheckBulletHit(characters[i].GetComponent<Character>().position, shootDirection[i]);
                characters[i].GetComponent<Character>().ShootBullet(shootDirection[i]);
            }
        }
    }
    private void CheckBulletHit(Vector2 startPos, Vector2 direction) {
        bool hit = false;
        Vector2 bulletPos = startPos;
        for(int i = 0; i < mapCreator.size && !hit; i++) {
            bulletPos += direction;
            for(int j = 0; j < characters.Count; j++){
                if (bulletPos == characters[j].GetComponent<Character>().position) {
                    characters[j].GetComponent<Character>().Die();
                    if(j <= 2){
                        playersScore[j]++;
                        print("Player Ganho!!!");
                    }
                    hit = true;
                    break;
                }
            }
        }
    }
    private void RandomizeCharactersMovement(){
        for(int i = playerQuantity; i < characters.Count; i++){
            Vector2 pos = characters[i].GetComponent<Character>().GetRandomVector2() + characters[i].GetComponent<Character>().position;
            AddOnDictionary(pos,characters[i]);
        }
    }
    private void AddOnDictionary(Vector2 vector, GameObject gameObject){
        if(nextPositions.ContainsKey(vector)){
            if(nextPositions[vector].GetComponent<Character>() != null) nextPositions.Remove(vector);
        }else{
            nextPositions.Add(vector,gameObject);
        }
    }
    private void AddObstaclesInDictionary(){
        for(int y = 0; y < mapCreator.size+2; y++){
			for(int x = 0; x < mapCreator.size+2; x++){
				if(mapCreator.map[x,y] == 1){
                    Vector2 v = new Vector2(x,y);
                    nextPositions.Add(v,this.gameObject);
                }
			}
		}
    }
    private void InitializeCharacters(){
        Vector2 pos = GetSpawnPoint();
        int x = (int)pos.x;
        int y = (int)pos.y;
        float tileSize = mapCreator.tileSize;
        float size = mapCreator.size + 2;
        Vector3 position = new Vector3((float)x, (float)y, 0) * tileSize;
        position -= new Vector3(size * tileSize - tileSize * 0.5f, size * tileSize - tileSize, 0) * 0.5f;

        var character = Instantiate(characterPrefab, position, Quaternion.identity);
        character.GetComponent<Character>().position = new Vector2(x, y);
        character.GetComponent<Character>().SetColor(colorOptions[Random.Range(0, colorOptions.Length)]);
        if (characters.Count == 0) { p1Icon = Instantiate(p1Icon, position + Vector3.up, Quaternion.identity, character.transform); }
        if (characters.Count == 1) { p2Icon = Instantiate(p2Icon, position + Vector3.up, Quaternion.identity, character.transform); }
        characters.Add(character);
    }

    private void SpawnAmmo() {
        Vector2 pos = GetSpawnPoint();
        int x = (int)pos.x;
        int y = (int)pos.y;
        Debug.Log("x: " + x + ",y: " + y);
        float tileSize = mapCreator.tileSize;
        float size = mapCreator.size + 2;
        Vector3 position = new Vector3((float)x, (float)y, 0) * tileSize;
        position -= new Vector3(size * tileSize - tileSize * 0.5f, size * tileSize - tileSize, 0) * 0.5f;

        var ammo = Instantiate(ammoPrefab, position, Quaternion.identity);
        ammo.GetComponent<Ammo>().position = new Vector2(x, y);
        ammos.Add(ammo);
    }
}
public enum MoveDirection {
    Up, Down, Right, Left
}

