//
// NormalMapMaker by mgear / UnityCoder.com
// url: http://unitycoder.com
// 

using UnityEditor;
using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.IO;

namespace unitycodercom_mapmaker2
{
		
	public class NormalMapMaker2 : EditorWindow
	{
		public static Texture2D sourceImage;
		private static int normalStrength = 5; // default 5
		private static int medianFilterStrength = 3; //  default 3
		private static bool normalGroupEnabled = true;
		private static bool specularGroupEnabled = true;
		private static float specularCutOff = 0.40f; // default 0.4
		private static float specularContrast = 1.5f; // default 1.5
		private static string appName = "NormalMapMaker";
		public static string normalSuffix = "_normal.png";
		public static string specularSuffix = "_specular.png";
		public static bool running = false;

		private const int previewSize = 164;
		private bool previewEnabled = true;
		private Texture2D previewNormalMap;
		private Texture2D previewSpecularMap;
		private const int previewTextureSize = 164;

		private Vector2 texViewScrollPosition = Vector2.zero;

		// create menu item and window
	    [MenuItem ("Window/Normal Map Maker2")]
	    static void Init () 
		{
	    NormalMapMaker2 window = (NormalMapMaker2)EditorWindow.GetWindow (typeof (NormalMapMaker2));
			window.titleContent = new GUIContent(appName);
			window.minSize = new Vector2(400,568);
			window.maxSize = new Vector2(400,570);
			running = true;
	    }
		
		// window closed
		void OnDestroy()
		{
			running = false;
		}
		
		// main loop/gui
		void OnGUI () 
		{
			SourceTextureGUI();
			SettingsGUI();
			EditorGUI.BeginChangeCheck();
			NormalGeneratorGUI();
			SpecularGeneratorGUI();
			EditorGUI.EndChangeCheck();

			if (GUI.changed) UpdatePreviews();

			GenerateButtonGUI();
		}

		// GUI's

		void SourceTextureGUI()
		{
			GUILayout.Label ("Source Texture", EditorStyles.boldLabel);
			EditorGUILayout.BeginHorizontal ();
			sourceImage = EditorGUILayout.ObjectField (sourceImage, typeof(Texture2D), false, GUILayout.Height (100)) as Texture2D;
			EditorGUILayout.EndHorizontal ();
			if (sourceImage != null) 
			{
				GUILayout.Label (sourceImage.name + "  (" + sourceImage.width + "x" + sourceImage.height + ")", EditorStyles.miniLabel);
			}else{
				GUILayout.Label ("", EditorStyles.miniLabel);
			}
		}	

		void SettingsGUI()
		{
			EditorGUILayout.BeginHorizontal ();
			GUILayout.FlexibleSpace();
			previewEnabled = EditorGUILayout.Toggle("Enable texture previews",previewEnabled);
			EditorGUILayout.EndHorizontal ();
			EditorGUILayout.Space();
		}

		void NormalGeneratorGUI()
		{
			normalGroupEnabled = EditorGUILayout.BeginToggleGroup ("Create Normal Map", normalGroupEnabled);
			EditorGUILayout.BeginHorizontal ();
			EditorGUILayout.BeginVertical ();
			normalStrength = EditorGUILayout.IntSlider ("Bumpiness Strength", normalStrength, 1, 20);
			medianFilterStrength = EditorGUILayout.IntSlider ("Median Filter Strength", medianFilterStrength, 0, 10);
			EditorGUILayout.EndVertical ();
			texViewScrollPosition = EditorGUILayout.BeginScrollView (texViewScrollPosition, GUILayout.Width (previewSize), GUILayout.Height (previewSize));
			if (previewEnabled)
			{
				var r = GUILayoutUtility.GetAspectRect(1.0f);
				if (previewNormalMap) EditorGUI.DrawPreviewTexture (r, previewNormalMap);
			}
			EditorGUILayout.EndScrollView ();
			EditorGUILayout.EndHorizontal ();
			EditorGUILayout.EndToggleGroup ();
			EditorGUILayout.Space ();
		}

		void SpecularGeneratorGUI ()
		{
			specularGroupEnabled = EditorGUILayout.BeginToggleGroup ("Create Specular Map", specularGroupEnabled);
			EditorGUILayout.BeginHorizontal ();
			EditorGUILayout.BeginVertical ();
			specularCutOff = EditorGUILayout.Slider ("Brightness Cutoff", specularCutOff, 0, 1);
			specularContrast = EditorGUILayout.Slider ("Specular Contrast", specularContrast, 0, 2);
			EditorGUILayout.EndVertical ();
			texViewScrollPosition = EditorGUILayout.BeginScrollView (texViewScrollPosition, GUILayout.Width (previewSize), GUILayout.Height (previewSize));
			if (previewEnabled)
			{
				var r2 = GUILayoutUtility.GetAspectRect (1.0f);
				if (sourceImage) EditorGUI.DrawPreviewTexture (r2, sourceImage);
			}
			EditorGUILayout.EndScrollView ();
			EditorGUILayout.EndHorizontal ();
			EditorGUILayout.EndToggleGroup ();
		}

		void GenerateButtonGUI ()
		{
			EditorGUILayout.Space ();
			GUI.enabled = sourceImage && (normalGroupEnabled || specularGroupEnabled) ? true : false;
			if (GUILayout.Button (new GUIContent ("Create Map" + (normalGroupEnabled && specularGroupEnabled ? "s" : ""), "Create Map" + (normalGroupEnabled && specularGroupEnabled ? "s" : "")), GUILayout.Height (40))) 
			{
				GenerateMaps();
			}
			GUI.enabled = true;
		}	


		void UpdatePreviews()
		{
			// TODO: normal / specs or both?

			if (sourceImage!=null) UpdateNormalMapPreview();

		}

		void UpdateNormalMapPreview()
		{

			if (previewNormalMap==null)
			{
				int textureWidth = sourceImage.width;
				int textureHeight = sourceImage.width;
				previewNormalMap = new Texture2D(textureWidth, textureHeight, TextureFormat.RGB24, false, false);
			}else{

				GenerateNormalMapPreview();

			}

		}

		void GenerateNormalMapPreview()
		{

			int textureWidth = sourceImage.width;
			int textureHeight = sourceImage.width;

			Texture2D texSource = new Texture2D(textureWidth, textureHeight, TextureFormat.RGB24, false, false);
			Color[] sourcePixels = sourceImage.GetPixels();
			texSource.SetPixels(sourcePixels);
			
			

			if (medianFilterStrength>=2) // must be atleast 2 for median filter
			{
				texSource.SetPixels(filtersMedian(sourceImage,medianFilterStrength,displayProgress:false));
			}
			
			Color[] pixels = new Color[textureWidth*textureHeight];
			// sobel filter
			Texture2D texNormal = new Texture2D(textureWidth, textureHeight, TextureFormat.RGB24, false, false);
			texNormal.hideFlags = HideFlags.HideAndDontSave;
			
			Vector3 vScale = new Vector3(0.3333f,0.3333f,0.3333f);
			for (int y=0;y<textureHeight;y++)
			{
				for (int x=0;x<textureWidth;x++)
				{
					// TODO: use pixels array for speed
					Color tc = texSource.GetPixel(x-1, y-1);
					Vector3 cSampleNegXNegY = new Vector3(tc.r,tc.g,tc.g);
					tc = texSource.GetPixel(x, y-1);
					Vector3 cSampleZerXNegY = new Vector3(tc.r,tc.g,tc.g);
					tc = texSource.GetPixel(x+1, y-1);
					Vector3 cSamplePosXNegY = new Vector3(tc.r,tc.g,tc.g);
					tc = texSource.GetPixel(x-1, y);
					Vector3 cSampleNegXZerY = new Vector3(tc.r,tc.g,tc.g);
					tc = texSource.GetPixel(x+1,y);
					Vector3 cSamplePosXZerY = new Vector3(tc.r,tc.g,tc.g);
					tc = texSource.GetPixel(x-1,y+1);
					Vector3 cSampleNegXPosY = new Vector3(tc.r,tc.g,tc.g);
					tc = texSource.GetPixel(x,y+1);
					Vector3 cSampleZerXPosY = new Vector3(tc.r,tc.g,tc.g);
					tc = texSource.GetPixel(x+1,y+1);
					Vector3 cSamplePosXPosY = new Vector3(tc.r,tc.g,tc.g);
					float fSampleNegXNegY = Vector3.Dot(cSampleNegXNegY, vScale);
					float fSampleZerXNegY = Vector3.Dot(cSampleZerXNegY, vScale);
					float fSamplePosXNegY = Vector3.Dot(cSamplePosXNegY, vScale);
					float fSampleNegXZerY = Vector3.Dot(cSampleNegXZerY, vScale);
					float fSamplePosXZerY = Vector3.Dot(cSamplePosXZerY, vScale);
					float fSampleNegXPosY = Vector3.Dot(cSampleNegXPosY, vScale);
					float fSampleZerXPosY = Vector3.Dot(cSampleZerXPosY, vScale);
					float fSamplePosXPosY = Vector3.Dot(cSamplePosXPosY, vScale);							
					float edgeX = (fSampleNegXNegY - fSamplePosXNegY) * 0.25f+(fSampleNegXZerY - fSamplePosXZerY) * 0.5f+ (fSampleNegXPosY - fSamplePosXPosY) * 0.25f;
					float edgeY = (fSampleNegXNegY - fSampleNegXPosY) * 0.25f+(fSampleZerXNegY - fSampleZerXPosY)*0.5f+(fSamplePosXNegY - fSamplePosXPosY)*0.25f;
					Vector2 vEdge = new Vector2(edgeX,edgeY)*normalStrength;
					Vector3 norm = new Vector3(vEdge.x,vEdge.y, 1.0f).normalized;
					Color c = new Color(norm.x*0.5f+0.5f,norm.y*0.5f+0.5f,norm.z*0.5f+0.5f,1);
					pixels[x+y*textureWidth] = c;
				}
	
			}
			
			previewNormalMap.SetPixels(pixels);
			previewNormalMap.Apply(false);

			
			UnityEngine.Object.DestroyImmediate(texSource);
			UnityEngine.Object.DestroyImmediate(texNormal);


		}




		// Generators

		void GenerateMaps()
		{
			bool setReadable = SetTextureTemporarilyReadableOn();

			if (specularGroupEnabled) GenerateSpecularMap();
			if (normalGroupEnabled) GenerateNormalMap();

			if (setReadable) SetTextureTemporarilyReadableOff();

			AssetDatabase.Refresh();
		}


		void GenerateNormalMap()
		{
			string baseFile = sourceImage.name;
			string path = AssetDatabase.GetAssetPath(sourceImage);
			
			int textureWidth = sourceImage.width;
			int textureHeight = sourceImage.width;
			
			float progress = 0.0f;
			float progressStep = 1.0f/textureHeight;

			Texture2D texSource = new Texture2D(textureWidth, textureHeight, TextureFormat.RGB24, false, false);
			Color[] sourcePixels = sourceImage.GetPixels();
			texSource.SetPixels(sourcePixels);


			progress = 0;
			if (medianFilterStrength>=2) // must be atleast 2 for median filter
			{
				texSource.SetPixels(filtersMedian(sourceImage,medianFilterStrength,displayProgress:true));
			}
			
			Color[] pixels = new Color[textureWidth*textureHeight];
			// sobel filter
			Texture2D texNormal = new Texture2D(textureWidth, textureHeight, TextureFormat.RGB24, false, false);
			texNormal.hideFlags = HideFlags.HideAndDontSave;
			
			Vector3 vScale = new Vector3(0.3333f,0.3333f,0.3333f);
			for (int y=0;y<textureHeight;y++)
			{
				for (int x=0;x<textureWidth;x++)
				{
					// TODO: use pixels array for speed
					Color tc = texSource.GetPixel(x-1, y-1);
					Vector3 cSampleNegXNegY = new Vector3(tc.r,tc.g,tc.g);
					tc = texSource.GetPixel(x, y-1);
					Vector3 cSampleZerXNegY = new Vector3(tc.r,tc.g,tc.g);
					tc = texSource.GetPixel(x+1, y-1);
					Vector3 cSamplePosXNegY = new Vector3(tc.r,tc.g,tc.g);
					tc = texSource.GetPixel(x-1, y);
					Vector3 cSampleNegXZerY = new Vector3(tc.r,tc.g,tc.g);
					tc = texSource.GetPixel(x+1,y);
					Vector3 cSamplePosXZerY = new Vector3(tc.r,tc.g,tc.g);
					tc = texSource.GetPixel(x-1,y+1);
					Vector3 cSampleNegXPosY = new Vector3(tc.r,tc.g,tc.g);
					tc = texSource.GetPixel(x,y+1);
					Vector3 cSampleZerXPosY = new Vector3(tc.r,tc.g,tc.g);
					tc = texSource.GetPixel(x+1,y+1);
					Vector3 cSamplePosXPosY = new Vector3(tc.r,tc.g,tc.g);
					float fSampleNegXNegY = Vector3.Dot(cSampleNegXNegY, vScale);
					float fSampleZerXNegY = Vector3.Dot(cSampleZerXNegY, vScale);
					float fSamplePosXNegY = Vector3.Dot(cSamplePosXNegY, vScale);
					float fSampleNegXZerY = Vector3.Dot(cSampleNegXZerY, vScale);
					float fSamplePosXZerY = Vector3.Dot(cSamplePosXZerY, vScale);
					float fSampleNegXPosY = Vector3.Dot(cSampleNegXPosY, vScale);
					float fSampleZerXPosY = Vector3.Dot(cSampleZerXPosY, vScale);
					float fSamplePosXPosY = Vector3.Dot(cSamplePosXPosY, vScale);							
					float edgeX = (fSampleNegXNegY - fSamplePosXNegY) * 0.25f+(fSampleNegXZerY - fSamplePosXZerY) * 0.5f+ (fSampleNegXPosY - fSamplePosXPosY) * 0.25f;
					float edgeY = (fSampleNegXNegY - fSampleNegXPosY) * 0.25f+(fSampleZerXNegY - fSampleZerXPosY)*0.5f+(fSamplePosXNegY - fSamplePosXPosY)*0.25f;
					Vector2 vEdge = new Vector2(edgeX,edgeY)*normalStrength;
					Vector3 norm = new Vector3(vEdge.x,vEdge.y, 1.0f).normalized;
					Color c = new Color(norm.x*0.5f+0.5f,norm.y*0.5f+0.5f,norm.z*0.5f+0.5f,1);
					pixels[x+y*textureWidth] = c;
				}
				progress+=progressStep;
				if(EditorUtility.DisplayCancelableProgressBar(appName,"Creating normal map..",progress)) 
				{
					Debug.Log(appName+": Normal map creation cancelled by user (strange texture results will occur)");
					EditorUtility.ClearProgressBar();
					break;
				}		
			}
			
			texNormal.SetPixels(pixels);

			byte[] saveToTextureBytes  = texNormal.EncodeToPNG();
			File.WriteAllBytes(Path.GetDirectoryName(path) + "/"+baseFile+normalSuffix, saveToTextureBytes);

			EditorUtility.ClearProgressBar();
			
			UnityEngine.Object.DestroyImmediate(texSource);
			UnityEngine.Object.DestroyImmediate(texNormal);

		}

		void GenerateSpecularMap()
		{
			string baseFile = sourceImage.name;
			string path = AssetDatabase.GetAssetPath(sourceImage);

			int textureWidth = sourceImage.width;
			int textureHeight = sourceImage.width;

			float progress = 0.0f;
			float progressStep = 1.0f/textureHeight;

			Texture2D texSpecular = new Texture2D(textureWidth, textureHeight, TextureFormat.RGB24, false, false);
			texSpecular.hideFlags = HideFlags.HideAndDontSave;
			Color[] pixels = new Color[textureWidth*textureHeight];
			for (int y=0;y<textureHeight;y++)
			{
				for (int x=0;x<textureWidth;x++)
				{
					float bw = sourceImage.GetPixel(x,y).grayscale;
					// adjust contrast
					bw *= bw * specularContrast;
					bw = bw<(specularContrast*specularCutOff)?-1:bw;
					bw = Mathf.Clamp(bw,-1,1);
					bw *= 0.5f;
					bw += 0.5f;
					Color c = new Color(bw,bw,bw,1);
					pixels[x+y*textureWidth] = c;
				}
				
				// progress bar
				progress+=progressStep;
				if(EditorUtility.DisplayCancelableProgressBar(appName,"Creating specular map..",progress)) 
				{
					Debug.Log(appName+": Specular map creation cancelled by user (strange texture results will occur)");
					EditorUtility.ClearProgressBar();
					break;
				}		
			}
			EditorUtility.ClearProgressBar();
			texSpecular.SetPixels(pixels);
			byte[] saveTextureBytes  = texSpecular.EncodeToPNG();
			File.WriteAllBytes(Path.GetDirectoryName(path) + "/" + baseFile+specularSuffix, saveTextureBytes);
			UnityEngine.Object.DestroyImmediate(texSpecular);
		}

		bool SetTextureTemporarilyReadableOn()
		{
			string path = AssetDatabase.GetAssetPath(sourceImage);

			TextureImporter textureImporter = AssetImporter.GetAtPath(path) as TextureImporter;
			if (textureImporter.isReadable == false)
			{
				textureImporter.isReadable = true;
				AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
				return false;
			}
			return true;
		}

		void SetTextureTemporarilyReadableOff()
		{
			string path = AssetDatabase.GetAssetPath(sourceImage);

			TextureImporter textureImporter = AssetImporter.GetAtPath(path) as TextureImporter;
			textureImporter.isReadable = false;
			AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
		}


		// ** helper functions **
		Color[] filtersMedian(Texture2D t, int fSize,bool displayProgress)
		{

			Color[] sourcePixels = t.GetPixels();

			float progress = 0.0f;
			float progressStep = 1.0f/t.height;
			Color[] medianTexture = new Color[t.width*t.height];
			int tIndex = 0;
			int medianMin = -(fSize/2);
			int medianMax = (fSize/2);
			List<float> r = new List<float>();
			List<float> g = new List<float>();
			List<float> b = new List<float>();

			for (int x = 0; x < t.width; ++x)
			{
				for (int y = 0; y < t.height; ++y)
				{
					r.Clear();
					g.Clear();
					b.Clear();
					for (int x2 = medianMin; x2 < medianMax; ++x2)
					{
						int tx = x + x2;
						if (tx >= 0 && tx < t.width)
						{
							for (int y2 = medianMin; y2 < medianMax; ++y2)
							{
								int ty = y + y2;
								if (ty >= 0 && ty < t.height)
								{
//									Color c = t.GetPixel(tx, ty);
									Color c = sourcePixels[tx+ty*t.width];
									r.Add(c.r);
									g.Add(c.g);
									b.Add(c.b);
								}
							}
						}
					}
					r.Sort();
					g.Sort();
					b.Sort();
					medianTexture[x+y*t.width]=new Color(r[r.Count/2],g[g.Count/2], b[b.Count/2]);
					tIndex++;
				}

				if (displayProgress)
				{
					progress+=progressStep;
					if(EditorUtility.DisplayCancelableProgressBar(appName,"Median filtering..",progress)) 
					{
						Debug.Log(appName+": Filtering cancelled by user (strange texture results will occur)");
						EditorUtility.ClearProgressBar();
						break;
					}
				}
			}
			if (displayProgress) EditorUtility.ClearProgressBar();
			return medianTexture;
		} // filtersMedian()

	} // class : normalmapmaker


	/*
	// helper class for setting texture type to NormalMap
	public class fixNormalMaps : AssetPostprocessor 
	{
		void OnPostprocessTexture (Texture2D texture)
		{
//			Debug.Log("1:"+NormalMapMaker2.running);
			// early exists if window not open or no source image selected
//			if (!NormalMapMaker2.running) return;
			Debug.Log("2");
			if (NormalMapMaker2.sourceImage.name==null) return;

//			Debug.Log("3");

			if (assetPath.Contains(NormalMapMaker2.sourceImage.name+NormalMapMaker2.normalSuffix))
			{
//				Debug.Log("4");


				// mark it as normal map texture type
				TextureImporter normalTextureImporter = assetImporter as TextureImporter;
				if (normalTextureImporter.textureType!=TextureImporterType.Bump)
				{
					normalTextureImporter.textureType = TextureImporterType.Bump;
//					Debug.Log((int)Mathf.Max(NormalMapMaker2.sourceImage.width,NormalMapMaker2.sourceImage.height));
					normalTextureImporter.maxTextureSize = (int)Mathf.Max(NormalMapMaker2.sourceImage.width,NormalMapMaker2.sourceImage.height);
					//AssetDatabase.Refresh();
				}
				// ping the created file
	//			EditorGUIUtility.PingObject(AssetDatabase.LoadMainAssetAtPath(assetPath)); // only works on 2nd time..but then asset already exists and we dont want to always ping there..
			}
	    }
	} // class : fixnormalmaps
*/

} // namespace
