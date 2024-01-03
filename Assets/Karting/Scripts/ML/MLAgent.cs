using Palmmedia.ReportGenerator.Core.Parser.Analysis;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using Unity.Plastic.Newtonsoft.Json;
using UnityEngine;
using static DecisionTree;

public class MLPParameters
{
    List<float[,]> coeficients;
    List<float[]> intercepts;

    public List<float[,]> Coeficients { get => coeficients; private set => coeficients = value; }
    public List<float[]> Intercepts { get => intercepts; private set => intercepts = value; }

    public MLPParameters(int numLayers)
    {
        coeficients = new List<float[,]>();
        intercepts = new List<float[]>();
        for (int i = 0; i < numLayers-1; i++)
        {
            coeficients.Add(null);
        }
        for (int i = 0; i < numLayers - 1; i++)
        {
            intercepts.Add(null);
        }
    }

    public void CreateCoeficient(int i, int rows, int cols)
    {
        coeficients[i] = new float[rows, cols];
    }

    public void SetCoeficiente(int i, int row, int col, float v)
    {
        coeficients[i][row, col] = v;
    }


    public void CreateIntercept(int i, int row)
    {
        intercepts[i] = new float[row];
    }

    public void SetIntercept(int i, int row, float v)
    {
        intercepts[i][row] = v;
    }
}

public class MLPModel
{
    MLPParameters mlpParameters;
    public MLPModel(MLPParameters p)
    {
        mlpParameters = p;
    }

    public float[] FeedForward(float[] input, Transform transform)
    {
        Debug.Log("Input: " + input.Length);

        int numLayers = mlpParameters.Coeficients.Count + 1;
        float[] layerOutput = input;

        for (int i = 0; i < numLayers - 1; i++)
        {
            layerOutput = PropagateLayer(layerOutput, mlpParameters.Coeficients[i], mlpParameters.Intercepts[i]);
        }

        return layerOutput;
    }

    private float[] PropagateLayer(float[] input, float[,] weights, float[] biases)
    {
        int numNeurons = biases.Length;
        int inputSize = input.Length;

        float[] output = new float[numNeurons];

        for (int i = 0; i < numNeurons; i++)
        {
            float neuronSum = 0;

            for (int j = 0; j < inputSize; j++)
            {
                neuronSum += input[j] * weights[j, i];
            }

            neuronSum += biases[i];

            output[i] = Sigmoid(neuronSum);
        }

        return output;
    }

    private float Sigmoid(float x)
    {
        return 1.0f / (1.0f + Mathf.Exp(-x));
    }

    /// <summary>
    /// Implements the conversion of the output value to the action label. 
    /// Depending on what actions you have chosen or saved in the dataset, and in what order, the way it is converted will be one or the other.
    /// </summary>
    /// <param name="index"></param>
    /// <returns></returns>
    public Labels ConvertIndexToLabel(int index)
    {
        Labels label = Labels.NONE;

        switch (index)
        {
            case 0:
                label = Labels.ACCELERATE;
                break;
            case 1:
                label = Labels.LEFT_ACCELERATE;
                break;
            case 2:
                label = Labels.RIGHT_ACCELERATE;
                break;
        }

        return label;
    }

    public Labels Predict(float[] output)
    {
        float max;
        int index = GetIndexMaxValue(output, out max);
        Labels label = ConvertIndexToLabel(index);
        return label;
    }

    public int GetIndexMaxValue(float[] output, out float max)
    {
        max = output[0];
        int index = 0;
        for(int i = 1; i < output.Length; i++)
        {
            if(output[i] > max)
            {
                max = output[i];
                index = i;
            }
        }
        return index;
    }
}

public class DecisionTree
{
    // Estructura para almacenar la información del árbol
    [System.Serializable]
    public class DecisionTreeStructure
    {
        public int n_nodes;
        public int[] children_left;
        public int[] children_right;
        public int[] feature;
        public float[] threshold;
        public float[][][] values;
    }

    private DecisionTreeStructure treeStructure;

    // Constructor que carga la estructura del árbol desde un archivo JSON
    public DecisionTree(string jsonContent)
    {
        treeStructure = JsonConvert.DeserializeObject<DecisionTreeStructure>(jsonContent);
    }

    // Método para realizar predicciones
    public int Predict(float[] sample)
    {
        int currentNode = 0;

        // Iterar a través del árbol hasta llegar a una hoja
        while (treeStructure.children_left[currentNode] != -1 && treeStructure.children_right[currentNode] != -1)
        {
            int featureIndex = treeStructure.feature[currentNode];
            float threshold = treeStructure.threshold[currentNode];

            // Comparar la característica de la muestra con el umbral en el nodo actual
            if (sample[featureIndex] <= threshold)
            {
                // Mover al hijo izquierdo
                currentNode = treeStructure.children_left[currentNode];
            }
            else
            {
                // Mover al hijo derecho
                currentNode = treeStructure.children_right[currentNode];
            }
        }

        // El nodo actual es una hoja, la clase predicha es el índice de la clase con mayor valor en 'value'
        int predictedClass = GetMaxValueIndex(treeStructure.values[currentNode][0]);

        return predictedClass;
    }

    private int GetMaxValueIndex(float[] array)
    {
        // Función para obtener el índice del máximo valor en un array
        int maxIndex = 0;
        float maxValue = array[0];

        for (int i = 1; i < array.Length; i++)
        {
            if (array[i] > maxValue)
            {
                maxIndex = i;
                maxValue = array[i];
            }
        }

        return maxIndex;
    }
}

public class ScalerParams 
{
    public float[] mean;
    public float[] std;
}

public class MLAgent : MonoBehaviour
{
    public enum ModelType { MLP=0, DT }
    public TextAsset text;
    public ModelType model;
    public bool agentEnable;

    private MLPParameters mlpParameters;
    private MLPModel mlpModel;
    private DecisionTree dtModel;
    private Perception perception;
    private ScalerParams scalerParams;

    // Start is called before the first frame update
    void Start()
    {
        if (agentEnable)
        {
            if (model == ModelType.MLP)
            {
                string file = text.text;
                mlpParameters = LoadParameters(file);
                mlpModel = new MLPModel(mlpParameters);
                Debug.Log("Parameters loaded " + mlpParameters);
            }
            else if (model == ModelType.DT)
            {
                string filePath = "Assets/MLModels/decision_tree_structure.json";

                // Verificar si el archivo existe
                if (File.Exists(filePath))
                {
                    // Leer el contenido del archivo JSON
                    string jsonContent = File.ReadAllText(filePath);

                    // Crear una instancia del árbol de decisión
                    dtModel = new DecisionTree(jsonContent);
                }
                else
                {
                    Debug.LogError("El archivo JSON del modelo de árbol de decisión no existe en la ruta especificada.");
                }
            }

            string paramsPath = "Assets/MLModels/scaler_params.json";
            // Verificar si el archivo existe
            if (File.Exists(paramsPath))
            {
                // Leer el contenido del archivo JSON
                string jsonContent = File.ReadAllText(paramsPath);

                scalerParams = JsonConvert.DeserializeObject<ScalerParams>(jsonContent);
            }
            else
            {
                Debug.LogError("El archivo JSON de los parametros de escalado no existe en la ruta especificada.");
            }

            perception = GetComponent<Perception>();
        }
    }

    public KartGame.KartSystems.InputData AgentInput()
    {
        Parameters parameters = Record.ReadParameters(8, Time.timeSinceLevelLoad, perception, transform);
        float[] inputParams = parameters.ConvertToFloatArrat();

        //quitamos el indice 6 que es kartY porque no lo usamos
        List<float> inputParamsList = new List<float>(inputParams);
        inputParamsList.RemoveAt(6);
        inputParams = inputParamsList.ToArray();

        //normalizamos
        for (int i = 0; i < inputParams.Length; i++) 
        {
            inputParams[i] = (inputParams[i] - scalerParams.mean[i]) / scalerParams.std[i]; 
        }

        Labels label = Labels.NONE;
        switch (model)
        {
            case ModelType.MLP:
                float[] outputs = this.mlpModel.FeedForward(inputParams,this.transform);
                label = this.mlpModel.Predict(outputs);
                break;
            case ModelType.DT:

                int result = dtModel.Predict(inputParams);

                switch (result) 
                {
                    case 0:
                        label = Labels.ACCELERATE;
                        break;
                    case 1:
                        label = Labels.LEFT_ACCELERATE;
                        break;
                    case 2:
                        label = Labels.RIGHT_ACCELERATE;
                        break;
                }

                break;
        }
        KartGame.KartSystems.InputData input = Record.ConvertLabelToInput(label);
        return input;
    }

    public static string TrimpBrackers(string val)
    {
        val = val.Trim();
        val = val.Substring(1);
        val = val.Substring(0, val.Length - 1);
        return val;
    }

    public static int[] SplitWithColumInt(string val)
    {
        val = val.Trim();
        string[] values =val.Split(",");
        int[] result = new int[values.Length];
        for(int i = 0; i < values.Length; i++)
        {
            values[i] = values[i].Trim();
            if (values[i].StartsWith("'"))
                values[i] = values[i].Substring(1);
            if (values[i].EndsWith("'"))
                values[i] = values[i].Substring(0, values[i].Length-1);
            result[i] = int.Parse(values[i]);
        }
        return result;
    }

    public static float[] SplitWithColumFloat(string val)
    {
        val = val.Trim();
        string[] values = val.Split(",");
        float[] result = new float[values.Length];
        for (int i = 0; i < values.Length; i++)
        {
            result[i] = float.Parse(values[i]);
        }
        return result;
    }

    public static MLPParameters LoadParameters(string file)
    {
        string[] lines = file.Split("\n");
        int num_layers = 0;
        MLPParameters mlpParameters = null;
        int currentParameter = -1;
        int[] currentDimension = null;
        bool coefficient = false;
        for (int i = 0; i < lines.Length; i++)
        {
            string line = lines[i];
            line = line.Trim();
            if(line != "")
            {
                string[] nameValue = line.Split(":");
                string name = nameValue[0];
                string val = nameValue[1];
                if (name == "num_layers")
                {
                    num_layers = int.Parse(val);
                    mlpParameters = new MLPParameters(num_layers);
                }
                else
                {
                    if (num_layers <= 0)
                        Debug.LogError("Format error: First line must be num_layers");
                    else
                    {
                        if (name == "parameter")
                            currentParameter = int.Parse(val);
                        else if (name == "dims")
                        {
                            val = TrimpBrackers(val);
                            currentDimension = SplitWithColumInt(val);
                        }
                        else if (name == "name")
                        {
                            if (val.StartsWith("coefficient"))
                            {
                                coefficient = true;
                                int index = currentParameter / 2;
                                mlpParameters.CreateCoeficient(currentParameter, currentDimension[0], currentDimension[1]);
                            }
                            else
                            {
                                coefficient = false;
                                mlpParameters.CreateIntercept(currentParameter, currentDimension[0]);
                            }

                        }
                        else if (name == "values")
                        {
                            val = TrimpBrackers(val);
                            float[] parameters = SplitWithColumFloat(val);

                            for (int index = 0; index < parameters.Length; index++)
                            {
                                if (coefficient)
                                {
                                    int row = index / currentDimension[1];
                                    int col = index % currentDimension[1];
                                    mlpParameters.SetCoeficiente(currentParameter, row, col, parameters[index]);
                                }
                                else
                                {
                                    mlpParameters.SetIntercept(currentParameter, index, parameters[index]);
                                }
                            }
                        }
                    }
                }
            }
        }
        return mlpParameters;
    }
}
