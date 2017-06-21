using UnityEngine;
using System.Collections.Generic;

using ProceduralNoiseProject;

namespace BrunetonsAtmosphere
{

    public class TerrainGenerator : MonoBehaviour
    {
        //Prototypes
        public Texture2D m_splat0, m_splat1;
        public float m_splatTileSize0 = 10.0f;
        public float m_splatTileSize1 = 2.0f;

        //Noise settings.
        public int m_mountainSeed = 1;
        public float m_mountainFrq = 0.002f;

        //Terrain settings
        public int m_tilesX = 2; //Number of terrain tiles on the x axis
        public int m_tilesZ = 2; //Number of terrain tiles on the z axis
        public float m_pixelMapError = 6.0f; //A lower pixel error will draw terrain at a higher Level of detail but will be slower
        public float m_baseMapDist = 1000.0f; //The distance at which the low res base map will be drawn. Decrease to increase performance

         //Terrain data settings
        public int m_heightMapSize = 513; //Higher number will create more detailed height maps
        public int m_alphaMapSize = 1024; //This is the control map that controls how the splat textures will be blended
        public int m_terrainSize = 2048;
        public int m_terrainHeight = 512;

        private FractalNoise m_mountainNoise;
        private Terrain[,] m_terrain;
        private SplatPrototype[] m_splatPrototypes;
        private Vector2 m_offset;

        void Start()
        {
            m_mountainNoise = new FractalNoise(new PerlinNoise(m_mountainSeed, m_mountainFrq), 6, 1.0f, 1.0f);

            if (!Mathf.IsPowerOfTwo(m_heightMapSize - 1))
            {
                Debug.Log("height map size must be pow2+1 number");
                m_heightMapSize = Mathf.ClosestPowerOfTwo(m_heightMapSize) + 1;
            }

            if (!Mathf.IsPowerOfTwo(m_alphaMapSize))
            {
                Debug.Log("Alpha map size must be pow2 number");
                m_alphaMapSize = Mathf.ClosestPowerOfTwo(m_alphaMapSize);
            }

            float[,] htmap = new float[m_heightMapSize, m_heightMapSize];

            m_terrain = new Terrain[m_tilesX, m_tilesZ];

            //this will center terrain at origin
            m_offset = new Vector2(-m_terrainSize*m_tilesX*0.5f, -m_terrainSize*m_tilesZ*0.5f);
       
            CreateProtoTypes();

            for (int x = 0; x < m_tilesX; x++)
            {
                for (int z = 0; z < m_tilesZ; z++)
                {
                    FillHeights(htmap, x, z);

                    TerrainData terrainData = new TerrainData();

                    terrainData.heightmapResolution = m_heightMapSize;
                    terrainData.SetHeights(0, 0, htmap);
                    terrainData.size = new Vector3(m_terrainSize, m_terrainHeight, m_terrainSize);
                    terrainData.splatPrototypes = m_splatPrototypes;

                    FillAlphaMap(terrainData);

                    m_terrain[x, z] = Terrain.CreateTerrainGameObject(terrainData).GetComponent<Terrain>();

                    m_terrain[x, z].transform.parent = transform;
                    m_terrain[x, z].transform.localPosition = new Vector3(m_terrainSize * x + m_offset.x, 0, m_terrainSize * z + m_offset.y);
                    m_terrain[x, z].heightmapPixelError = m_pixelMapError;
                    m_terrain[x, z].basemapDistance = m_baseMapDist;
                    

                }
            }

            //Set the neighbours of terrain to remove seams.
            for (int x = 0; x < m_tilesX; x++)
            {
                for (int z = 0; z < m_tilesZ; z++)
                {
                    Terrain right = null;
                    Terrain left = null;
                    Terrain bottom = null;
                    Terrain top = null;

                    if (x > 0) left = m_terrain[(x - 1), z];
                    if (x < m_tilesX - 1) right = m_terrain[(x + 1), z];

                    if (z > 0) bottom = m_terrain[x, (z - 1)];
                    if (z < m_tilesZ - 1) top = m_terrain[x, (z + 1)];

                    m_terrain[x, z].SetNeighbors(left, top, right, bottom);

                }
            }

        }

        void CreateProtoTypes()
        {
            m_splatPrototypes = new SplatPrototype[2];

            m_splatPrototypes[0] = new SplatPrototype();
            m_splatPrototypes[0].texture = m_splat0;
            m_splatPrototypes[0].tileSize = new Vector2(m_splatTileSize0, m_splatTileSize0);

            m_splatPrototypes[1] = new SplatPrototype();
            m_splatPrototypes[1].texture = m_splat1;
            m_splatPrototypes[1].tileSize = new Vector2(m_splatTileSize1, m_splatTileSize1);

        }

        void FillHeights(float[,] htmap, int tileX, int tileZ)
        {
            float ratio = (float)m_terrainSize / (float)m_heightMapSize;

            for (int x = 0; x < m_heightMapSize; x++)
            {
                for (int z = 0; z < m_heightMapSize; z++)
                {
                    float worldPosX = (x + tileX * (m_heightMapSize - 1)) * ratio;
                    float worldPosZ = (z + tileZ * (m_heightMapSize - 1)) * ratio;

                    htmap[z, x] = Mathf.Max(0.0f, m_mountainNoise.Sample2D(worldPosX, worldPosZ));
                }
            }
        }

        void FillAlphaMap(TerrainData terrainData)
        {
            float[,,] map = new float[m_alphaMapSize, m_alphaMapSize, 2];

            Random.InitState(0);

            for (int x = 0; x < m_alphaMapSize; x++)
            {
                for (int z = 0; z < m_alphaMapSize; z++)
                {
                    // Get the normalized terrain coordinate that
                    // corresponds to the the point.
                    float normX = x * 1.0f / (m_alphaMapSize - 1);
                    float normZ = z * 1.0f / (m_alphaMapSize - 1);

                    // Get the steepness value at the normalized coordinate.
                    float angle = terrainData.GetSteepness(normX, normZ);

                    // Steepness is given as an angle, 0..90 degrees. Divide
                    // by 90 to get an alpha blending value in the range 0..1.
                    float frac = angle / 90.0f;
                    map[z, x, 0] = frac;
                    map[z, x, 1] = 1.0f - frac;

                }
            }

            terrainData.alphamapResolution = m_alphaMapSize;
            terrainData.SetAlphamaps(0, 0, map);
        }


    }

}


