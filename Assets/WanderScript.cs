﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Rnd = UnityEngine.Random;

public class WanderScript : MonoBehaviour
{
    public KMBombModule Module;
    public KMBombInfo BombInfo;
    public KMAudio Audio;

    public KMSelectable[] ArrowSels;
    public KMSelectable MiddleSel;
    public GameObject[] WallObjs;
    public GameObject[] VertexObjs;
    public Material[] WallMats;
    public GameObject[] StarObjs;
    public GameObject MazeParent;
    public TextMesh AliveCountText;
    public TextMesh GoalText;
    public AudioSource ActionAudio;

    private int _moduleId;
    private static int _moduleIdCounter = 1;
    private bool _moduleSolved;

    private bool[][] _originalWalls = new bool[9][] { new bool[4], new bool[5], new bool[4], new bool[5], new bool[4], new bool[5], new bool[4], new bool[5], new bool[4] };
    private bool[][] _visitedCells = new bool[4][] { new bool[4], new bool[4], new bool[4], new bool[4] };
    private bool[][] _transformedWalls = new bool[9][] { new bool[4], new bool[5], new bool[4], new bool[5], new bool[4], new bool[5], new bool[4], new bool[5], new bool[4] };
    private int _wallColor;
    private int[] _currentPositions;
    private bool[] _deadPositions;
    private List<int> _actionHistory = new List<int>();
    private bool _isAnimating;
    private int _aliveCount;
    private int _goal;
    private bool _canMove;

    private void Start()
    {
        _moduleId = _moduleIdCounter++;
        for (int btn = 0; btn < ArrowSels.Length; btn++)
            ArrowSels[btn].OnInteract += ArrowPress(btn);
        MiddleSel.OnInteract += MiddlePress;
        Debug.LogFormat("[Wander #{0}] 404 Logging not found!", _moduleId);
        Setup();
    }

    private bool MiddlePress()
    {
        if (_moduleSolved || _isAnimating)
            return false;
        if (!_canMove)
        {
            Audio.PlaySoundAtTransform("MiddlePress", transform);
            _aliveCount = 16;
            AliveCountText.text = _aliveCount.ToString();
            MazeParent.SetActive(false);
            AliveCountText.gameObject.SetActive(true);
            StartCoroutine(PulseObject(AliveCountText.gameObject, new Vector3(0.002f, 0.002f, 0.002f)));
            StartCoroutine(PulseObject(GoalText.gameObject, new Vector3(0.001f, 0.001f, 0.001f)));
            _canMove = true;
            return false;
        }
        _isAnimating = true;
        StartCoroutine(ShowActionHistory());
        return false;
    }

    private KMSelectable.OnInteractHandler ArrowPress(int btn)
    {
        return delegate ()
        {
            Audio.PlayGameSoundAtTransform(KMSoundOverride.SoundEffect.ButtonRelease, transform);
            ArrowSels[btn].AddInteractionPunch(0.5f);
            if (_moduleSolved || _isAnimating || !_canMove)
                return false;
            _actionHistory.Add(btn);
            _aliveCount = 0;
            StartCoroutine(PulseObject(AliveCountText.gameObject, new Vector3(0.002f, 0.002f, 0.002f)));
            StartCoroutine(PulseObject(GoalText.gameObject, new Vector3(0.001f, 0.001f, 0.001f)));
            for (int i = 0; i < _currentPositions.Length; i++)
            {
                if (!_deadPositions[i] && CheckValidMove(_currentPositions[i], btn))
                {
                    _aliveCount++;
                    _currentPositions[i] = btn == 0 ? (_currentPositions[i] - 4) : btn == 1 ? (_currentPositions[i] + 1) : btn == 2 ? (_currentPositions[i] + 4) : (_currentPositions[i] - 1);
                }
                else
                    _deadPositions[i] = true;
            }
            AliveCountText.text = _aliveCount.ToString();
            if (_aliveCount == 1)
                GoalText.gameObject.SetActive(true);
            if (_aliveCount == 0)
            {
                _isAnimating = true;
                StartCoroutine(ShowActionHistory());
            }
            return false;
        };
    }

    private void Setup()
    {
        AliveCountText.gameObject.SetActive(false);
        GoalText.gameObject.SetActive(false);
        _goal = Rnd.Range(0, 16);
        GoalText.text = GetCoord(_goal);
        for (int i = 0; i < _originalWalls.Length; i++)
            for (int j = 0; j < _originalWalls[i].Length; j++)
                _originalWalls[i][j] = true;
        for (int i = 0; i < StarObjs.Length; i++)
            StarObjs[i].SetActive(false);
        _deadPositions = new bool[16];
        _currentPositions = Enumerable.Range(0, 16).ToArray();
        var x = Rnd.Range(0, 4);
        var y = Rnd.Range(0, 4);
        GenerateMaze(x, y);
        _wallColor = Rnd.Range(0, 8);
        DoMazeTransformations();
    }

    private void GenerateMaze(int x, int y)
    {
        _visitedCells[x][y] = true;
        var arr = Enumerable.Range(0, 4).ToArray().Shuffle();
        for (int i = 0; i < 4; i++)
        {
            switch (arr[i])
            {
                case 0:
                    if (y != 0 && !_visitedCells[x][y - 1])
                    {
                        _originalWalls[y * 2][x] = false;
                        GenerateMaze(x, y - 1);
                    }
                    break;
                case 1:
                    if (x != 3 && !_visitedCells[x + 1][y])
                    {
                        _originalWalls[y * 2 + 1][x + 1] = false;
                        GenerateMaze(x + 1, y);
                    }
                    break;
                case 2:
                    if (y != 3 && !_visitedCells[x][y + 1])
                    {
                        _originalWalls[y * 2 + 2][x] = false;
                        GenerateMaze(x, y + 1);
                    }
                    break;
                default:
                    if (x != 0 && !_visitedCells[x - 1][y])
                    {
                        _originalWalls[y * 2 + 1][x] = false;
                        GenerateMaze(x - 1, y);
                    }
                    break;
            }
        }
    }

    private void DoMazeTransformations()
    {
        _transformedWalls = SetTempWalls(_originalWalls);
        var tempWalls = SetTempWalls(_originalWalls);
        if ((_wallColor & 4) == 4)
        {
            for (int i = 0; i < _transformedWalls.Length; i++)
                for (int j = 0; j < _transformedWalls[i].Length; j++)
                    _transformedWalls[i][j] = tempWalls[i][(_transformedWalls[i].Length - 1) - j];
            tempWalls = SetTempWalls(_transformedWalls);
        }
        if ((_wallColor & 2) == 2)
        {
            for (int i = 0; i < _transformedWalls.Length; i++)
                for (int j = 0; j < _transformedWalls[i].Length; j++)
                    _transformedWalls[i][j] = tempWalls[(_transformedWalls.Length - 1) - i][j];
        }
        if ((_wallColor & 1) == 1)
        {
            var str = "";
            str += "#" + (_transformedWalls[0][0] ? "#" : "-") + "#" + (_transformedWalls[0][1] ? "#" : "-") + "#" + (_transformedWalls[0][2] ? "#" : "-") + "#" + (_transformedWalls[0][3] ? "#" : "-") + "#";
            str += (_transformedWalls[1][0] ? "#" : "-") + "-" + (_transformedWalls[1][1] ? "#" : "-") + "-" + (_transformedWalls[1][2] ? "#" : "-") + "-" + (_transformedWalls[1][3] ? "#" : "-") + "-" + (_transformedWalls[1][4] ? "#" : "-") + "";
            str += "#" + (_transformedWalls[2][0] ? "#" : "-") + "#" + (_transformedWalls[2][1] ? "#" : "-") + "#" + (_transformedWalls[2][2] ? "#" : "-") + "#" + (_transformedWalls[2][3] ? "#" : "-") + "#";
            str += (_transformedWalls[3][0] ? "#" : "-") + "-" + (_transformedWalls[3][1] ? "#" : "-") + "-" + (_transformedWalls[3][2] ? "#" : "-") + "-" + (_transformedWalls[3][3] ? "#" : "-") + "-" + (_transformedWalls[3][4] ? "#" : "-") + "";
            str += "#" + (_transformedWalls[4][0] ? "#" : "-") + "#" + (_transformedWalls[4][1] ? "#" : "-") + "#" + (_transformedWalls[4][2] ? "#" : "-") + "#" + (_transformedWalls[4][3] ? "#" : "-") + "#";
            str += (_transformedWalls[5][0] ? "#" : "-") + "-" + (_transformedWalls[5][1] ? "#" : "-") + "-" + (_transformedWalls[5][2] ? "#" : "-") + "-" + (_transformedWalls[5][3] ? "#" : "-") + "-" + (_transformedWalls[5][4] ? "#" : "-") + "";
            str += "#" + (_transformedWalls[6][0] ? "#" : "-") + "#" + (_transformedWalls[6][1] ? "#" : "-") + "#" + (_transformedWalls[6][2] ? "#" : "-") + "#" + (_transformedWalls[6][3] ? "#" : "-") + "#";
            str += (_transformedWalls[7][0] ? "#" : "-") + "-" + (_transformedWalls[7][1] ? "#" : "-") + "-" + (_transformedWalls[7][2] ? "#" : "-") + "-" + (_transformedWalls[7][3] ? "#" : "-") + "-" + (_transformedWalls[7][4] ? "#" : "-") + "";
            str += "#" + (_transformedWalls[8][0] ? "#" : "-") + "#" + (_transformedWalls[8][1] ? "#" : "-") + "#" + (_transformedWalls[8][2] ? "#" : "-") + "#" + (_transformedWalls[8][3] ? "#" : "-") + "#";
            _transformedWalls = SetTempWalls(GetSwappedWalls(str));
        }
        ShowMaze();
    }

    private bool[][] SetTempWalls(bool[][] walls)
    {
        var tempWalls = new bool[9][] { new bool[4], new bool[5], new bool[4], new bool[5], new bool[4], new bool[5], new bool[4], new bool[5], new bool[4] };
        for (int i = 0; i < walls.Length; i++)
            for (int j = 0; j < walls[i].Length; j++)
                tempWalls[i][j] = walls[i][j];
        return tempWalls;
    }

    private void ShowMaze()
    {
        var str = "";
        LogMaze(_originalWalls, false);
        LogMaze(_transformedWalls, true);
        for (int i = 0; i < _transformedWalls.Length; i++)
            for (int j = 0; j < _transformedWalls[i].Length; j++)
                str += _transformedWalls[i][j] ? "#" : ".";
        for (int i = 0; i < str.Length; i++)
        {
            WallObjs[i].SetActive(str[i] == '#');
            WallObjs[i].GetComponent<MeshRenderer>().material = WallMats[_wallColor];
        }
        for (int i = 0; i < VertexObjs.Length; i++)
            VertexObjs[i].GetComponent<MeshRenderer>().material = WallMats[_wallColor];
    }

    private void LogMaze(bool[][] walls, bool transformed)
    {
        // Debug.LogFormat("[Wander #{0}] Maze walls, {1}:", _moduleId, transformed ? "after transformation" : "before transformation");
        // Debug.LogFormat("[Wander #{0}] {1}", _moduleId, GetMazeString(walls, true));
    }

    private string GetMazeString(bool[][] walls, bool newLines)
    {
        var str = "";
        str += string.Format("#{0}#{1}#{2}#{3}#{4}", walls[0][0] ? "#" : "-", walls[0][1] ? "#" : "-", walls[0][2] ? "#" : "-", walls[0][3] ? "#" : "-", newLines ? "\n" : "");
        str += string.Format("{0}-{1}-{2}-{3}-{4}{5}", walls[1][0] ? "#" : "-", walls[1][1] ? "#" : "-", walls[1][2] ? "#" : "-", walls[1][3] ? "#" : "-", walls[1][4] ? "#" : "-", newLines ? "\n" : "");
        str += string.Format("#{0}#{1}#{2}#{3}#{4}", walls[2][0] ? "#" : "-", walls[2][1] ? "#" : "-", walls[2][2] ? "#" : "-", walls[2][3] ? "#" : "-", newLines ? "\n" : "");
        str += string.Format("{0}-{1}-{2}-{3}-{4}{5}", walls[3][0] ? "#" : "-", walls[3][1] ? "#" : "-", walls[3][2] ? "#" : "-", walls[3][3] ? "#" : "-", walls[1][4] ? "#" : "-", newLines ? "\n" : "");
        str += string.Format("#{0}#{1}#{2}#{3}#{4}", walls[4][0] ? "#" : "-", walls[4][1] ? "#" : "-", walls[4][2] ? "#" : "-", walls[4][3] ? "#" : "-", newLines ? "\n" : "");
        str += string.Format("{0}-{1}-{2}-{3}-{4}{5}", walls[5][0] ? "#" : "-", walls[5][1] ? "#" : "-", walls[5][2] ? "#" : "-", walls[5][3] ? "#" : "-", walls[1][4] ? "#" : "-", newLines ? "\n" : "");
        str += string.Format("#{0}#{1}#{2}#{3}#{4}", walls[6][0] ? "#" : "-", walls[6][1] ? "#" : "-", walls[6][2] ? "#" : "-", walls[6][3] ? "#" : "-", newLines ? "\n" : "");
        str += string.Format("{0}-{1}-{2}-{3}-{4}{5}", walls[7][0] ? "#" : "-", walls[7][1] ? "#" : "-", walls[7][2] ? "#" : "-", walls[7][3] ? "#" : "-", walls[1][4] ? "#" : "-", newLines ? "\n" : "");
        str += string.Format("#{0}#{1}#{2}#{3}#", walls[8][0] ? "#" : "-", walls[8][1] ? "#" : "-", walls[8][2] ? "#" : "-", walls[8][3] ? "#" : "-");
        return str;
    }

    private bool[][] GetSwappedWalls(string str)
    {
        var walls = new bool[9][] { new bool[4], new bool[5], new bool[4], new bool[5], new bool[4], new bool[5], new bool[4], new bool[5], new bool[4] };
        for (int i = 0; i < walls.Length; i++)
        {
            for (int j = 0; j < walls[i].Length; j++)
            {
                if (i % 2 == 0)
                    walls[i][j] = str[(j * 18 + 9) + i] == '#';
                else
                    walls[i][j] = str[(j * 18) + i] == '#';
            }
        }
        return walls;
    }

    private void Reset()
    {
        MazeParent.SetActive(true);
        _canMove = false;
        _originalWalls = new bool[9][] { new bool[4], new bool[5], new bool[4], new bool[5], new bool[4], new bool[5], new bool[4], new bool[5], new bool[4] };
        _visitedCells = new bool[4][] { new bool[4], new bool[4], new bool[4], new bool[4] };
        _transformedWalls = new bool[9][] { new bool[4], new bool[5], new bool[4], new bool[5], new bool[4], new bool[5], new bool[4], new bool[5], new bool[4] };
        _actionHistory = new List<int>();
        _isAnimating = false;
        Setup();
    }

    private bool CheckValidMove(int num, int dir)
    {
        var pos = num / 4 * 16 + 8 + (num / 4 * 2) + num % 4 * 2 + 2;
        var walls = GetMazeString(_originalWalls, false);
        if (dir == 0)
            return walls[pos - 9] == '-';
        if (dir == 1)
            return walls[pos + 1] == '-';
        if (dir == 2)
            return walls[pos + 9] == '-';
        else
            return walls[pos - 1] == '-';
    }

    private IEnumerator ShowActionHistory()
    {
        ActionAudio.Play();
        GoalText.gameObject.SetActive(false);
        AliveCountText.gameObject.SetActive(false);
        var current = Enumerable.Range(0, 16).ToArray();
        var dead = new bool[16];
        for (int i = 0; i < StarObjs.Length; i++)
            StarObjs[i].SetActive(true);
        if (_actionHistory.Count == 0)
        {
            yield return new WaitForSeconds(0.964f);
            for (int i = 0; i < 16; i++)
            {
                StarObjs[i].SetActive(true);
                StartCoroutine(PulseObject(StarObjs[i], new Vector3(0.15f, 0.15f, 0.15f)));
            }
        }
        else
        {
            for (int a = 0; a < _actionHistory.Count; a++)
            {
                yield return new WaitForSeconds(0.964f);
                var alive = new List<int>();
                for (int i = 0; i < current.Length; i++)
                {
                    if (!dead[i] && CheckValidMove(current[i], _actionHistory[a]))
                    {
                        current[i] = _actionHistory[a] == 0 ? (current[i] - 4) : _actionHistory[a] == 1 ? (current[i] + 1) : _actionHistory[a] == 2 ? (current[i] + 4) : (current[i] - 1);
                        alive.Add(current[i]);
                    }
                    else
                        dead[i] = true;
                }
                for (int i = 0; i < 16; i++)
                {
                    StarObjs[i].SetActive(alive.Contains(i));
                    StartCoroutine(PulseObject(StarObjs[i], new Vector3(0.15f, 0.15f, 0.15f)));
                }
            }
        }
        ActionAudio.Stop();
        if (_aliveCount == 0)
        {
            // Debug.LogFormat("[Maze Manual Challenge #{0}] All positions have died. Strike.", _moduleId);
            Module.HandleStrike();
            Reset();
            yield break;
        }
        if (_aliveCount == 1)
        {
            int curPos = -1;
            for (int i = 0; i < _currentPositions.Length; i++)
                if (!_deadPositions[i])
                    curPos = _currentPositions[i];
            if (curPos == _goal)
            {
                _moduleSolved = true;
                Module.HandlePass();
                Audio.PlaySoundAtTransform("Solve", transform);
                MazeParent.SetActive(true);
                var str = "";
                for (int i = 0; i < _originalWalls.Length; i++)
                    for (int j = 0; j < _originalWalls[i].Length; j++)
                        str += _originalWalls[i][j] ? "#" : ".";
                for (int i = 0; i < str.Length; i++)
                {
                    WallObjs[i].SetActive(str[i] == '#');
                    WallObjs[i].GetComponent<MeshRenderer>().material = WallMats[_wallColor];
                }
                for (int i = 0; i < VertexObjs.Length; i++)
                    VertexObjs[i].GetComponent<MeshRenderer>().material = WallMats[_wallColor];
                // Debug.LogFormat("[Maze Manual Challenge #{0}] Successfully submitted at position {1}. Module solved.", _moduleId, GetCoord(curPos));
                for (int i = 0; i < StarObjs.Length; i++)
                    StarObjs[i].GetComponent<MeshRenderer>().material = WallMats[2];
                yield break;
            }
            else
            {
                // Debug.LogFormat("[Maze Manual Challenge #{0}] Incorrectly submitted at position {1}. Strike.", _moduleId, GetCoord(curPos));
                Module.HandleStrike();
                Reset();
            }
        }
        else
        {
            // Debug.LogFormat("[Maze Manual Challenge #{0}] Attempted to submit when there were multiple live positions. Strike.", _moduleId);
            Module.HandleStrike();
            Reset();
        }
    }

    private IEnumerator PulseObject(GameObject obj, Vector3 scale)
    {
        var duration = 0.2f;
        var elapsed = 0f;
        while (elapsed < duration)
        {
            obj.transform.localScale = new Vector3(Easing.InOutQuad(elapsed, scale.x * 1.2f, scale.x, duration), Easing.InOutQuad(elapsed, scale.y * 1.2f, scale.y, duration), Easing.InOutQuad(elapsed, scale.z * 1.2f, scale.z, duration));
            yield return null;
            elapsed += Time.deltaTime;
        }
        obj.transform.localScale = new Vector3(scale.x, scale.y, scale.z);
    }

    private string GetCoord(int num)
    {
        return "ABCD"[num % 4].ToString() + "1234"[num / 4].ToString();
    }

#pragma warning disable 0414
    private readonly string TwitchHelpMessage = "!There is an ongoing manual challenge!";
#pragma warning restore 0414

    private IEnumerator ProcessTwitchCommand(string command)
    {
        yield return null;
        yield return "sendtochat There is an ongoing manual challenge!";
        yield break;
    }

    private IEnumerator TwitchHandleForcedSolve()
    {
        _moduleSolved = true;
        Module.HandlePass();
        yield break;
    }
}