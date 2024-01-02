using KartGame.KartSystems;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;


public class Parameters
{
    public float[] parametersValue;
    public float timeStamp;
    public Parameters(int numParameters, float time)
    {
        parametersValue = new float[numParameters];
        timeStamp = time;
    }

    public float this[int index]
    {
        get
        {
            return parametersValue[index];
        }

        set
        {
            parametersValue[index] = value;
        }
        // get and set accessors
        
    }

    public int Size
    {
        get
        {
            return parametersValue.Length;
        }
    }

    public int Length
    {
        get
        {
            return parametersValue.Length;
        }
    }

    public float[] ConvertToFloatArrat()
    {
        float[] ret = new float[parametersValue.Length + 1];
        for(int i = 0; i < parametersValue.Length; i++)
        {
            ret[i] = parametersValue[i];
        }
        ret[parametersValue.Length] = timeStamp;
        return ret;
    }

    public override string ToString()
    {
        string s = "";
        for (int j = 0; j < Length; j++)
        {
            string f = parametersValue[j].ToString();
            f = f.Replace(",", ".");
            s += f + ",";
        }
        string tf = timeStamp.ToString();
        tf = tf.Replace(",", ".");
        s += tf;
        return s;
    }
}

public enum Labels { NONE=0, ACCELERATE=1, BRAKE=2, LEFT_ACCELERATE=3, RIGHT_ACCELERATE=4, LEFT_BRAKE=5, RIGHT_BRAKE=6 }
public class Record : MonoBehaviour
{
    public bool recordMode;
    public Perception perception;
    public Transform kart;
    public KeyboardInput keyboardInput;
    public string[] perceptionNames;
    public float snapshotTime;
    public string csvOutput;
    public bool saveWhenFinish;

    private string[] parametersName;
    private List<Parameters> parameters;
    private List<Labels> labels;
    private float time;
    private float totalTime;

    // Start is called before the first frame update
    void Start()
    {
        parametersName = new string[perceptionNames.Length + 3];
        for (int i = 0; i < perceptionNames.Length; i++)
        {
            parametersName[i] = perceptionNames[i];
        }
        parametersName[perceptionNames.Length] = "kartx";
        parametersName[perceptionNames.Length + 1] = "karty";
        parametersName[perceptionNames.Length + 2] = "kartz";
        parameters = new List<Parameters>();
        labels = new List<Labels>();
        time = 0f;
        totalTime = 0f;
    }

    // Update is called once per frame
    void Update()
    {
        totalTime += Time.deltaTime;
        if (Input.GetKeyDown(KeyCode.R))
        {
            recordMode = !recordMode;
        }

        if (recordMode)
        {
            time += Time.deltaTime;
            if(time > snapshotTime)
            {
                time = time - snapshotTime;
                RecordSnapshot(totalTime);
            }
        }
    }

    public void RecordSnapshot(float t)
    {
        Parameters p = ReadParameters(parametersName.Length, t,perception, kart);
        parameters.Add(p);
        InputData input = keyboardInput.GenerateInput();
        labels.Add(ConvertToLabel(input));
    }

    public static Labels ConvertToLabel(InputData input)
    {
        if (input.Accelerate)
        {
            if (input.TurnInput < 0f)
            {
                return Labels.LEFT_ACCELERATE;
            }
            else if (input.TurnInput > 0f)
            {
                return Labels.RIGHT_ACCELERATE;
            }
            else
                return Labels.ACCELERATE;
        }
        else if (input.Brake)
        {
            if (input.TurnInput < 0f)
            {
                return Labels.LEFT_BRAKE;
            }
            else if (input.TurnInput > 0f)
            {
                return Labels.RIGHT_BRAKE;
            }
            else
                return Labels.BRAKE;
        }
        else
            return Labels.NONE;
    }


    public static InputData ConvertLabelToInput(Labels label)
    {
        InputData inputData = new InputData();
        inputData.Accelerate = false;
        inputData.Brake = false;
        inputData.TurnInput = 0f;
        switch (label)
        {
            case Labels.ACCELERATE:
                inputData.Accelerate = true;
                break;
            case Labels.BRAKE:
                inputData.Brake = true;
                break;
            case Labels.LEFT_ACCELERATE:
                inputData.Accelerate = true;
                inputData.TurnInput = -1f;
                break;
            case Labels.RIGHT_ACCELERATE:
                inputData.Accelerate = true;
                inputData.TurnInput = 1f;
                break;
            case Labels.LEFT_BRAKE:
                inputData.Brake = true;
                inputData.TurnInput = -1f;
                break;
            case Labels.RIGHT_BRAKE:
                inputData.Brake = true;
                inputData.TurnInput = 1f;
                break;
        }

        return inputData;
    }

    private void OnDestroy()
    {
        if(saveWhenFinish && recordMode)
        {
            string csvFormat = ConvertToCSV(parametersName, parameters, labels);
            File.WriteAllText(csvOutput, csvFormat);
            Debug.Log("File "+ csvOutput + " save");
        }
    }

    public static string ConvertToCSV(string[] parametersName,List<Parameters> parameters,List<Labels> labels)
    {
        string csv = "";
        for(int i = 0; i < parametersName.Length; i++)
        {
            csv += parametersName[i] + ",";
        }
        csv += "time,";
        csv += "action\n";

        for (int i = 0; i < parameters.Count; i++)
        {
            Parameters p = parameters[i];
            csv += p.ToString();
            csv += ",";
            csv += labels[i].ToString() + "\n";
        }

        return csv;
    }

    public static Parameters ReadParameters(int numParameters, float t, Perception perception, Transform kart)
    {
        Parameters p = new Parameters(numParameters, t);
        PerceptionInfo[] perceptionInfo = perception.Perceptions;
        for (int i = 0; i < perceptionInfo.Length; i++)
        {
            if (perceptionInfo[i].detected)
            {
                p[i] = perceptionInfo[i].hit.distance;
            }
            else
            {
                p[i] = -1f;
            }
        }
        p[perceptionInfo.Length] = kart.position.x;
        p[perceptionInfo.Length + 1] = kart.position.y;
        p[perceptionInfo.Length + 2] = kart.position.z;
        return p;
    }

}
