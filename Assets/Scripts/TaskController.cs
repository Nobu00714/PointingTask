using System.Diagnostics.Tracing;
using System.Xml.Schema;
using System.Data.Common;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.IO;
using System.Text;
using uWintab;
using UnityEngine.SceneManagement;

public class TaskController : MonoBehaviour
{
    [SerializeField] public int Participant;
    [SerializeField] public Bias bias;
    [SerializeField] private Team team;
    [SerializeField] public Device device;
    [SerializeField] private bool practice;
    [SerializeField] public int set;
    [SerializeField] public int taskNum;
    [SerializeField] public int biasNum;
    [SerializeField] private int nowTaskAmplitude;
    [SerializeField] private int nowTaskWidth;
    [SerializeField] GameObject start;
    [SerializeField] GameObject goal;
    [SerializeField] GameObject button;
    [SerializeField] private AudioClip correctAudio;
    [SerializeField] private AudioClip wrongAudio;
    [SerializeField] private GameObject cursor;
    private Tablet tablet;
    private bool taskReady;
    private bool taskStarted;
    private bool startCrossed;
    private bool goalPressed;
    private bool nextButtonPressed;
    private bool press;
    [SerializeField] public int[] taskAmplitude;
    [SerializeField] public int[] taskWidth;
    private List<int> taskList;
    private StreamWriter swPos;
    private StreamWriter swMT;
    private float taskStartTime;
    private float taskFinishTime;
    private float mouseX;
    private float mouseY;   
    private bool falseStart = false;
    private SpriteRenderer startRenderer;
    private SpriteRenderer goalRenderer;
    private AudioSource audioSource;
    private float Ae;
    private float AeOld;
    private Vector2 StartPoint;
    private Vector2 EndPoint;
    private float DistanceToTargetCenter;
    private float mouseXprev;
    private float mouseYprev;
    private int taskClear;
    private int sameTaskNum;
    RectTransform rectTransform;
    private bool buttonPressable = true;
    GameObject trajestory;
    GameObject trajestoryLine;
    [SerializeField] GameObject cursorTrajestoryParent;
    [SerializeField] LineRenderer lineRenderer;
    GameObject LineInstance;
    private bool first = true;
    private bool CSVUpdated = false;
    private bool firstPenTouch =true;
    private bool firstPenRelease =true;
    private List<Vector3> trajectoryList = new List<Vector3>();
    public enum Bias
    {
        Fast,
        Neutral,
        Accurate
    }
    public enum Team
    {
        FastToAccurate,
        AccurateToFast
    }
    public enum Device
    {
        Mouse,
        Pen
    }
    void Start()
    {
        Application.targetFrameRate = 120;
        startRenderer = start.GetComponent<SpriteRenderer>();
        goalRenderer = goal.GetComponent<SpriteRenderer>();
        audioSource = this.GetComponent<AudioSource>();
        rectTransform = button.GetComponent<RectTransform>();
        tablet = this.GetComponent<Tablet>();
        taskList = new List<int>();
        for(int i=0; i<taskAmplitude.Length*taskWidth.Length; i++)
        {
            taskList.Add(i);
        }
        ShuffleList(taskList);

        taskUpdate();
        
        Cursor.visible = false;
        trajestory = (GameObject)Resources.Load("CursorTrajestory");
        trajestoryLine = (GameObject)Resources.Load("Line");

        bias = VariableManager.bias;
        biasNum = VariableManager.biasNum;
        Participant = VariableManager.Participant;
        taskNum = VariableManager.taskNum;
        VariableManager.MTSum = 0;
        swMT = VariableManager.swMT;
        VariableManager.ERSum = 0;
    }
    void OnApplicationQuit()
    {
        swMT.Close();
    }
    void Update()
    {
        DecideCursorPos();
        DrawCursorTrajestory();
        if(taskNum == 0)
        {
            if(first)
            {
                makeMTCSV();
                first = false;
                VariableManager.swMT = swMT;
            }
        }

        float startX = nowTaskAmplitude/2;
        float startY = 0;
        float startYUp = nowTaskWidth/2;
        float startYBottom = -nowTaskWidth/2;
        float startXRight = nowTaskAmplitude/2 + nowTaskWidth/2;
        float startXLeft = nowTaskAmplitude/2 - nowTaskWidth/2;
        float goalX = -nowTaskAmplitude/2;
        float goalY = 0;
        float goalXRight = - nowTaskAmplitude/2 + nowTaskWidth/2;
        float goalXLeft = - nowTaskAmplitude/2 - nowTaskWidth/2;
        float goalYUp = nowTaskWidth/2;
        float goalYBottom = -nowTaskWidth/2;

        //ネクストボタンを押したら
        if(Input.GetMouseButtonDown(0) && buttonPressable && mouseX>=rectTransform.anchoredPosition.x-250 && mouseX<=rectTransform.anchoredPosition.x+250 && mouseY>=rectTransform.anchoredPosition.y-250 && mouseY<=rectTransform.anchoredPosition.y+250)
        {
            startRenderer.color = new Color(1f,1f,1f,1f);   //開始ターゲットを白色にする
            goalRenderer.color = new Color(1f,1f,1f,1f);   //終了ターゲットを白色にする
            if(goalPressed)
            {
                taskNum++;
                taskUpdate();
            }
            nextButtonPressed = true;
            button.SetActive(false);
            buttonPressable = false;
            goalPressed = false;
        }

        //タスク開始済みなら終了判定受付及び，軌跡の記録
        if(taskStarted)
        {
            updatePosCSV(); //軌跡の保存
            //軌跡総距離を記録
            Ae += Mathf.Sqrt(Mathf.Pow(mouseX-mouseXprev,2)+Mathf.Pow(mouseY-mouseYprev,2));
            //もう一度マウスかペンを押したら
            if(Input.GetMouseButtonDown(0) || tablet.pressure>0.001)
            {
                taskFinishTime = Time.time; //タスク終了時刻を記録
                taskStarted = false; //タスク開始済みフラグをFalseに
                goalPressed = true; //ゴール済みフラグをTrueに
                DistanceToTargetCenter = mouseX-goalX; //ターゲット中心からクリック座標までの水平距離を記録
                //終了ターゲット内をクリックしたら
                if(mouseX>=goalXLeft && mouseX<=goalXRight)
                {
                    goalRenderer.color = new Color(0f,1f,0f,1f); //終了ターゲットを緑色にする
                    taskClear = 1; //クリアを記録
                    audioSource.PlayOneShot(correctAudio); //クリア音を鳴らす
                    
                }
                //終了ターゲット外をクリックしたら
                else
                {
                    goalRenderer.color = new Color(1f,0f,0f,1f);   //終了ターゲットを赤色にする
                    taskClear = 0; //エラーを記録
                    audioSource.PlayOneShot(wrongAudio); //ビープ音を鳴らす
                }
                //Nextボタンをランダムな高さに表示する
                rectTransform.anchoredPosition = new Vector3(-750,UnityEngine.Random.Range(-250,250),0);
                button.SetActive(true);
                buttonPressable = true;
                //MTのデータのCSVを更新
                updateMTCSV(taskClear);
                swPos.Close();
                //軌跡を削除
                foreach ( Transform child in cursorTrajestoryParent.transform )
                {
                    GameObject.Destroy(child.gameObject);
                }
            }
        }

        //ネクストボタンが押されたら準備完了
        if(nextButtonPressed)
        {
            nextButtonPressed = false;
            taskReady = true;
            CSVUpdated = false;
        }

        //タスク準備完了なら開始処理
        if(taskReady)
        {
            //マウスかペンを押したら
            if(Input.GetMouseButtonDown(0) || tablet.pressure>0.001)
            {
                //ターゲット内を押したら
                if(mouseX<=startXRight && mouseX>=startXLeft)
                {
                    makePosCSV();
                    Ae = 0f; //軌跡総距離の記録をリセット
                    startRenderer.color = new Color(0f,1f,0f,1f);   //開始ターゲットを緑色にする
                    taskStartTime = Time.time; //タスク開始時刻を記録
                    taskStarted = true; //タスクスタート済みフラグをTrueに
                    taskReady = false; //タスク準備完了フラグをFalseに
                }
                //ターゲット外を押したら
                else
                {
                    // startRenderer.color = new Color(1f,0f,0f,1f);   //赤色にする
                    // falseStart = true;
                }
            }
        }
        
        


        ChangeBias(); //条件を満たしたら，バイアスを変更
        ShowSetResult(); //セットが終わったらそのセットの結果を表示
        FinishExperiment(); //条件を満たしたら実験を終了

        mouseXprev = mouseX;
        mouseYprev = mouseY;
    }
    //ボタンが押されたら
    public void OnClick()
    {
        startRenderer.color = new Color(1f,1f,1f,1f);   //白色にする
        goalRenderer.color = new Color(1f,1f,1f,1f);   //白色にする
        if(goalPressed)
        {
            taskNum++;
            taskUpdate();
        }
        nextButtonPressed = true;
        button.SetActive(false);
        goalPressed = false;
    }
    //カーソルの位置を決定
    private void DecideCursorPos()
    {
        if(device == Device.Pen)
        {
            mouseX = tablet.x * Screen.width - Screen.width/2;
            mouseY = tablet.y * Screen.height - Screen.height/2;
        }
        if(device == Device.Mouse)
        {
            mouseX = Input.mousePosition.x - Screen.width/2;
            mouseY = Input.mousePosition.y - Screen.height/2;
        }
        cursor.GetComponent<RectTransform>().anchoredPosition = new Vector3(mouseX, mouseY, 1);
        //Debug.Log("X:"+mouseX+"Y:"+mouseY);
    }
    //カーソルの軌跡を描画
    private void DrawCursorTrajestory()
    {
        if(press)
        {
            //GameObject instance = (GameObject)Instantiate(trajestory, cursor.GetComponent<RectTransform>().anchoredPosition, Quaternion.identity);
            //instance.transform.parent = cursorTrajestoryParent.transform;
            LineRenderer lineRenderer = LineInstance.GetComponent<LineRenderer>();
            //trajectoryList.Add(cursor.GetComponent<RectTransform>().anchoredPosition);
            trajectoryList.Add(new Vector2(mouseX, mouseY));
            var positions = new Vector3[trajectoryList.Count];
            for(int i=0; i<trajectoryList.Count; i++)
            {
                positions[i] = trajectoryList[i];
            }
            lineRenderer.positionCount = trajectoryList.Count;
            lineRenderer.material = new Material(Shader.Find("Sprites/Default"));
            lineRenderer.startColor = new Color(0,0,1,1);
            lineRenderer.endColor = new Color(0,0,1,1);
            lineRenderer.SetPositions(positions);
        }
    }
    private void ShowSetResult()
    {
        if(taskNum%15 == 0 && taskNum != 0 && !VariableManager.resultCheck)
        {
            VariableManager.taskNum = taskNum;
            SceneManager.LoadScene("SetResultScene");
        }
        if(taskNum%15 == 1)
        {
            VariableManager.resultCheck = false;
        }
    }
    //バイアスを変更
    private void ChangeBias()
    {
        if(taskNum>=set*taskWidth.Length*taskAmplitude.Length)
        {
            taskNum = 0;
            first = true;
            biasNum++;
            VariableManager.biasNum = biasNum;
            swMT.Close();
            if(team == Team.FastToAccurate)
            {
                if(biasNum == 1)
                {
                    bias = Bias.Fast;
                    VariableManager.bias = bias;
                    SceneManager.LoadScene("ToFastScene");
                }
                if(biasNum == 2)
                {
                    bias = Bias.Accurate;
                    VariableManager.bias = bias;
                    SceneManager.LoadScene("ToControlScene");
                }
            }
            if(team == Team.AccurateToFast)
            {
                if(biasNum == 1)
                {
                    bias = Bias.Accurate;
                    VariableManager.bias = bias;
                    SceneManager.LoadScene("ToControlScene");
                }
                if(biasNum == 2)
                {
                    bias = Bias.Fast;
                    VariableManager.bias = bias;
                    SceneManager.LoadScene("ToFastScene");
                }
            }
        }
    }
    //タスクがすべて終わったら終了
    private void FinishExperiment()
    {
        if(biasNum>2)
        {   
            #if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;//ゲームプレイ終了
            #else
                Application.Quit();//ゲームプレイ終了
            #endif
        }
    }
    //タスクの順番をシャッフル
    private void ShuffleList(List<int> list)
    {
        int tmp;
        int rndNum;
        for(int i=list.Count-1; i>1; i--)
        {
            rndNum = UnityEngine.Random.Range(0,i);
            tmp = list[rndNum];
            list[rndNum] = list[i];
            list[i] = tmp;
        }
    }
    //タスクの更新
    private void taskUpdate()
    {
        nowTaskWidth = taskWidth[taskList[taskNum%(taskAmplitude.Length*taskWidth.Length)]%taskWidth.Length];
        nowTaskAmplitude = taskAmplitude[taskList[taskNum%(taskAmplitude.Length*taskWidth.Length)]/taskWidth.Length];
        start.transform.localScale = new Vector3(nowTaskWidth,Screen.currentResolution.height,1);
        goal.transform.localScale = new Vector3(nowTaskWidth,Screen.currentResolution.height,1);
        start.transform.position = new Vector3((nowTaskAmplitude/2)-nowTaskWidth/2,Screen.currentResolution.height/2,0);
        goal.transform.position = new Vector3(-(nowTaskAmplitude/2)-nowTaskWidth/2,Screen.currentResolution.height/2,0);
    }
    //マウス座標を保存するCSVを作成
    private void makePosCSV()
    {
        if(practice)
        {
            swPos = new StreamWriter(@"PracticePos"+Participant.ToString()+bias.ToString()+"No."+taskNum+"A"+nowTaskAmplitude+"W"+nowTaskWidth+".csv", true, Encoding.GetEncoding("UTF-8"));
        }
        else
        {
            swPos = new StreamWriter(@"Pos"+Participant.ToString()+bias.ToString()+"No."+taskNum+"A"+nowTaskAmplitude+"W"+nowTaskWidth+"Num"+sameTaskNum+".csv", true, Encoding.GetEncoding("UTF-8"));
        }
        string[] s1 = { "参加者", "長さ", "幅", "時間", "x座標", "y座標"};
        string s2 = string.Join(",", s1);
        swPos.WriteLine(s2);
    }
    //フレームごとにマウス座標を保存
    private void updatePosCSV()
    {
        string[] s1 = {Participant.ToString(),nowTaskAmplitude.ToString(),nowTaskWidth.ToString(),(Time.time-taskStartTime).ToString(),mouseX.ToString(),mouseY.ToString()};
        string s2 = string.Join(",",s1);
        if(swPos!=null)
        {
            swPos.WriteLine(s2);
        }
    }
    //操作時間を保存するCSVを作成
    private void makeMTCSV()
    {
        if(practice)
        {
            swMT = new StreamWriter(@"PracticeMT"+Participant.ToString()+bias.ToString()+".csv", true, Encoding.GetEncoding("UTF-8"));
        }
        else
        {
            swMT = new StreamWriter(@"MT"+Participant.ToString()+bias.ToString()+".csv", true, Encoding.GetEncoding("UTF-8"));
        }
        string[] s1 = { "参加者", "セット", "試行", "長さ", "幅","バイアス", "操作時間", "ゴールずれ","旧Ae","軌跡総距離","クリア" };
        string s2 = string.Join(",", s1);
        swMT.WriteLine(s2);
    }
    //タスクのクリアごとに操作時間を保存
    private void updateMTCSV(int clear)
    {
        AeOld = Mathf.Abs(EndPoint.x-StartPoint.x);
        string[] s1 = {Participant.ToString(), (taskNum/(taskAmplitude.Length*taskWidth.Length)).ToString(), (taskNum%(taskAmplitude.Length*taskWidth.Length)).ToString(), nowTaskAmplitude.ToString(), nowTaskWidth.ToString(), bias.ToString(), (taskFinishTime-taskStartTime).ToString(), DistanceToTargetCenter.ToString(), AeOld.ToString(), Ae.ToString(), clear.ToString()};
        string s2 = string.Join(",", s1);
        if(swMT!=null)
        {
            swMT.WriteLine(s2);
        }
        if(clear==1 || clear == 0)
        {
            VariableManager.MTSum += taskFinishTime-taskStartTime;
            if(bias == Bias.Neutral && taskNum/(taskAmplitude.Length*taskWidth.Length)>=3)
            {
                VariableManager.AllMTSumNeutral += taskFinishTime-taskStartTime;
            }
            if(bias == Bias.Fast)
            {
                VariableManager.AllMTSumFast += taskFinishTime-taskStartTime;
            }
            if(bias == Bias.Accurate)
            {
                VariableManager.AllMTSumAccurate += taskFinishTime-taskStartTime;
            }
        }
        
    }
}