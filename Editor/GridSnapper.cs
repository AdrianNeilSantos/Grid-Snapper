using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEngine.Rendering;


// RENAME: Grid Snapper
public class GridSnapperTool : EditorWindow{


    private const float GUI_SPACING = 10f;
    const float TAU = Mathf.PI * 2;

    
    // Tool params
    public Color color = Color.white;
    [Range(0f, 1f)]
    public float opacity = 1f;
    [Range(1f, 10f)]
    public float thickness = 1f;
    public bool gridEnabled = true;
    public enum GridType{
        Cartesian, Polar
    }

    public GridType gridType = GridType.Cartesian;
    [Range(1f, 10f)]
    public float gridSize = 1f;

    [Range(16f, 100f)]
    public float gridDrawExtent = 16f;
    public int angularDivisions = 24; 



    // Serialized params
    SerializedObject so;
    SerializedProperty propColor;
    SerializedProperty propThickness;
    SerializedProperty propOpacity;
    SerializedProperty propGridEnabled;
    

    SerializedProperty propGridType;
    SerializedProperty propGridSize;
    SerializedProperty propGridDrawExtent;
    SerializedProperty propAngularDivisions;



    [MenuItem("Tools/Grid Snapper")]
    public static void ShowWindow() => GetWindow<GridSnapperTool>("Grid Snapper");



    void OnEnable(){
        so = new SerializedObject(this);
        propGridSize = so.FindProperty("gridSize");
        propColor = so.FindProperty("color");
        propThickness = so.FindProperty("thickness");
        propGridType = so.FindProperty("gridType");
        propGridEnabled = so.FindProperty("gridEnabled");
        propOpacity = so.FindProperty("opacity");
        propGridDrawExtent = so.FindProperty("gridDrawExtent");
        propAngularDivisions = so.FindProperty("angularDivisions");

        LoadConfigurations();

        Selection.selectionChanged += Repaint;
        SceneView.duringSceneGui += DuringSceneGUI;
    }

    void OnDisable(){

        SaveConfigurations();



        Selection.selectionChanged -= Repaint;
        SceneView.duringSceneGui -= DuringSceneGUI;
    }

    void DuringSceneGUI(SceneView sceneView){
        if(gridEnabled){
            ControlGridSize();
            ControlGridDrawExtent();
            ControlGridType();
            ControlSnapSelection();

            Handles.zTest = CompareFunction.LessEqual;
            Handles.color = new Color(color.r, color.g, color.b, opacity);

            if(gridType == GridType.Cartesian){
                DrawGridCartesian();
            }
            else{
                DrawGridPolar();
            }
            Handles.color = Color.white;
        }
    }




    private void OnGUI() {

        so.Update();
        GUILayout.Space(GUI_SPACING);

        // Category 0: Descriptions
        using(new GUILayout.VerticalScope(EditorStyles.helpBox)){
            EditorGUILayout.LabelField("Controls", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("Toggle Grid Type: G");
            EditorGUILayout.LabelField("Change Grid Size: Alt + Scroll Wheel");
            EditorGUILayout.LabelField("Change Grid Draw Extent: Ctrl + Scroll Wheel ");
            EditorGUILayout.LabelField("Snap Selection: Ctrl + Alt + S");
        }

        GUILayout.Space(GUI_SPACING); 
        // Category 1: Appearance
        using(new GUILayout.VerticalScope(EditorStyles.helpBox) ){
            EditorGUILayout.LabelField("Appearance", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(propColor);
            EditorGUILayout.PropertyField(propOpacity);
            EditorGUILayout.PropertyField(propThickness);
            EditorGUILayout.PropertyField(propGridEnabled);
        }


        GUILayout.Space(GUI_SPACING); 
        // Category 2: Grid Settings
        using(new EditorGUI.DisabledScope(!gridEnabled)){
            using(new GUILayout.VerticalScope(EditorStyles.helpBox)){
                EditorGUILayout.LabelField("Grid Settings", EditorStyles.boldLabel);
                EditorGUILayout.PropertyField(propGridType);
                EditorGUILayout.PropertyField(propGridSize);
                EditorGUILayout.PropertyField(propGridDrawExtent);
                if(gridType == GridType.Polar){
                    EditorGUILayout.PropertyField(propAngularDivisions);
                    propAngularDivisions.intValue = Mathf.Max(4, propAngularDivisions.intValue);
                }
            }
        }
        so.ApplyModifiedProperties();

        // Snap Button
        GUILayout.Space(GUI_SPACING); 
        using(new EditorGUI.DisabledScope(Selection.gameObjects.Length == 0)){
            if(GUILayout.Button("Snap Selection")){
                SnapSelection();
            }
        }


    }






    private void DrawGridCartesian(){
        int lineCount = Mathf.RoundToInt((gridDrawExtent*2) / gridSize);
        if(lineCount % 2 == 0){
            lineCount++;
        }
        int halfLineCount = lineCount / 2;

        for(int i = 0; i < lineCount; i++){
            float intOffset = i - halfLineCount;

            float xCoord = intOffset * gridSize;
            float zCoord0 = halfLineCount * gridSize;
            float zCoord1 = -halfLineCount * gridSize;
            Vector3 p0 = new Vector3(xCoord, 0f, zCoord0);
            Vector3 p1 = new Vector3(xCoord, 0f, zCoord1);
            Handles.DrawAAPolyLine(thickness, p0, p1);
            p0 = new Vector3(zCoord0, 0f, xCoord);
            p1 = new Vector3(zCoord1, 0f, xCoord);
            Handles.DrawAAPolyLine(thickness, p0, p1);
        }
    }


    private void DrawGridPolar(){
        int ringCount = Mathf.RoundToInt(gridDrawExtent / gridSize);

        float radiusOuter = (ringCount-1) * gridSize;

        // radial grid lines
        for(int i = 1; i < ringCount; i++){
            float radius = i*gridSize;
            Handles.DrawWireDisc(Vector3.zero, Vector3.up, radius, thickness);
        }

        // angular grid lines
        for(int i = 0; i < angularDivisions; i++){
            float t = i / (float) angularDivisions;
            float angRad = t * TAU; // turns to radians
            float x = Mathf.Cos(angRad);
            float z = Mathf.Sin(angRad);
            Vector3 dir = new Vector3(x, 0f, z);
            Handles.DrawAAPolyLine(thickness, Vector3.zero, dir * radiusOuter);

        }

    }


    void SnapSelection(){
        foreach(GameObject go in Selection.gameObjects){
            Undo.RecordObject(go.transform, "snap objects");
            go.transform.position = GetSnappedPosition(go.transform.position);
        }
    }


    Vector3 GetSnappedPosition(Vector3 posOriginal){
        if(gridType == GridType.Cartesian){
            return Round(posOriginal, gridSize);
        }

        if(gridType == GridType.Polar){
            Vector2 vec = new Vector2(posOriginal.x, posOriginal.z);
            float distance = vec.magnitude;
            float distanceSnapped = Round(distance, gridSize);

            float angRad = Mathf.Atan2(vec.y, vec.x); // o to TAU
            float angTurns = angRad / TAU; // o to 1
            float angTurnsSnapped = Round(angTurns, 1f/angularDivisions);
            float angRadSnapped = angTurnsSnapped * TAU;

            Vector2 snappedDir = new Vector2(Mathf.Cos(angRadSnapped), Mathf.Sin(angRadSnapped));
            Vector2 snappedVec = snappedDir * distanceSnapped;

            return new Vector3(snappedVec.x, posOriginal.y, snappedVec.y);
        }

        return default;

    }


    private void LoadConfigurations(){
        color = new Color(EditorPrefs.GetFloat("GRID_SNAPPER_color_R", 1f),
                    EditorPrefs.GetFloat("GRID_SNAPPER_color_G", 1f),
                    EditorPrefs.GetFloat("GRID_SNAPPER_color_B", 1f),
                    1f);
        opacity = EditorPrefs.GetFloat("GRID_SNAPPER_opacity", 1f);
        thickness = EditorPrefs.GetFloat("GRID_SNAPPER_thickness", 1f);
        gridEnabled = EditorPrefs.GetInt("GRID_SNAPPER_grid_enabled", 1) != 0;

        gridType = (GridType) EditorPrefs.GetInt("GRID_SNAPPER_gridType", 0);
        gridSize = EditorPrefs.GetFloat("GRID_SNAPPER_gridSize", 1f);
        gridDrawExtent = EditorPrefs.GetFloat("GRID_SNAPPER_gridDrawExtent", 16f);
        angularDivisions = EditorPrefs.GetInt("GRID_SNAPPER_angularDivisions", 24);
    }


    private void SaveConfigurations(){
        EditorPrefs.SetFloat("GRID_SNAPPER_color_R", color.r);
        EditorPrefs.SetFloat("GRID_SNAPPER_color_G", color.g);
        EditorPrefs.SetFloat("GRID_SNAPPER_color_B", color.b);
        
        EditorPrefs.SetFloat("GRID_SNAPPER_opacity", opacity);
        EditorPrefs.SetFloat("GRID_SNAPPER_thickness", thickness);
        EditorPrefs.SetInt("GRID_SNAPPER_grid_enabled", (gridEnabled ? 1 : 0));

        EditorPrefs.SetInt("GRID_SNAPPER_gridType", (gridType == GridType.Cartesian ? 0: 1));
        EditorPrefs.SetFloat("GRID_SNAPPER_gridSize", gridSize);
        EditorPrefs.SetFloat("GRID_SNAPPER_gridDrawExtent", gridDrawExtent);
        EditorPrefs.SetInt("GRID_SNAPPER_angularDivisions", angularDivisions);
    }


    private Vector3 Round(Vector3 v){
        v.x = Mathf.Round(v.x);
        v.y = Mathf.Round(v.y);
        v.z = Mathf.Round(v.z);
        return v;
    }

    private Vector3 Round(Vector3 v, float size){
        return Round(v/size) * size;
    }

    private float Round(float v, float size){
        return Mathf.Round(v/size)*size;
    }

    void ControlGridSize(){
        bool holdingAlt = (Event.current.modifiers & EventModifiers.Alt) != 0;

        // Change radius
        if(Event.current.type == EventType.ScrollWheel && holdingAlt){
            float scrollDir = Mathf.Sign(Event.current.delta.y);
            so.Update();
            float scaleSpeed = 0.05f;
            propGridSize.floatValue *= 1f + scrollDir * scaleSpeed; 
            so.ApplyModifiedProperties();
            Repaint(); // updates the editor window
            Event.current.Use(); // consume the event
        }
    }

    void ControlGridDrawExtent(){
        bool holdingCtrl = (Event.current.modifiers & EventModifiers.Control) != 0;
        // Change spawnCount
        if(Event.current.type == EventType.ScrollWheel && holdingCtrl){
            float scrollDir = Mathf.Sign(Event.current.delta.y);
            so.Update();
            float scaleSpeed = 0.05f;
            propGridDrawExtent.floatValue *= 1f + scrollDir * scaleSpeed; 
            so.ApplyModifiedProperties();
            Repaint(); // updates the editor window
            Event.current.Use(); // consume the event
        }
    }

    void ControlGridType(){
        if(Event.current.type == EventType.KeyDown && Event.current.keyCode == KeyCode.G){
            so.Update();
            propGridType.intValue = (propGridType.intValue == 0) ? 1 : 0; // Toggle between 0 and 1
            Debug.Log(gridType);
            so.ApplyModifiedProperties();
            Event.current.Use(); // consume the event
        }
    }

    void ControlSnapSelection(){
        bool holdingCtrl = (Event.current.modifiers & EventModifiers.Control) != 0;
        bool holdingAlt = (Event.current.modifiers & EventModifiers.Alt) != 0;
        if(holdingCtrl && holdingAlt && Event.current.type == EventType.KeyDown && Event.current.keyCode == KeyCode.S){
            SnapSelection();
        }

    }


}
