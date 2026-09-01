#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.Rendering;
using UnityEditor.Events;
using UnityEditor.Animations;
using BeastSoccer.Data;
using BeastSoccer.Core;
using BeastSoccer.Player;
using BeastSoccer.Ball;
using BeastSoccer.AI;
using BeastSoccer.Input;
using BeastSoccer.UI;
using BeastSoccer.Audio;
using BeastSoccer.Presentation;
using BeastSoccer.CameraSystem;

namespace BeastSoccer.EditorTools
{
    public static class SceneBuilder
    {
        private const string ScenesPath="Assets/Scenes";

        [MenuItem("Beast Soccer/Repair FIX26 Presentation On Open Match (Preserve Art)")]
        public static void RepairFix26Presentation()
        {
            var scene = SceneManager.GetActiveScene();
            if (!scene.IsValid()) return;

            var visualsGO = GameObject.Find("VISUALS_3D");
            if (visualsGO == null) visualsGO = new GameObject("VISUALS_3D");
            var env = visualsGO.transform.Find("Environment_ATTACH_OR_REPLACE_3D_MODELS");
            if (env == null)
            {
                var envGO = new GameObject("Environment_ATTACH_OR_REPLACE_3D_MODELS");
                envGO.transform.SetParent(visualsGO.transform, false);
                env = envGO.transform;
            }

            var pitch = env.Find("Pitch_Artwork");
            if (pitch == null)
            {
                var pitchGO = GameObject.CreatePrimitive(PrimitiveType.Plane);
                pitchGO.name = "Pitch_Artwork";
                pitchGO.transform.SetParent(env, false);
                pitchGO.transform.localScale = new Vector3(2.4f, 1f, 1.586f);
                pitchGO.GetComponent<Renderer>().sharedMaterial = MakePitchArtworkMaterial();
                UnityEngine.Object.DestroyImmediate(pitchGO.GetComponent<Collider>());
            }
            else
            {
                pitch.localScale = new Vector3(2.4f, 1f, 1.586f);
                var r = pitch.GetComponent<Renderer>();
                if (r != null) r.sharedMaterial = MakePitchArtworkMaterial();
            }

            EnsureGoalRootForRepair(env, +1);
            EnsureGoalRootForRepair(env, -1);

            var cam = Camera.main;
            if (cam != null)
            {
                cam.fieldOfView = 46f;
                if (cam.GetComponent<FollowCamera>() == null) cam.gameObject.AddComponent<FollowCamera>();
            }

            EditorSceneManager.MarkSceneDirty(scene);
            Debug.Log("FIX26 presentation repaired without rebuilding Match: pitch artwork/goal attach roots/camera support restored. Existing goal FBXs and other custom children were preserved. Save the scene when it looks right.");
        }

        private static void EnsureGoalRootForRepair(Transform env, int side)
        {
            string name = side > 0 ? "Goal_PosX_ATTACH_PREFAB_HERE" : "Goal_NegX_ATTACH_PREFAB_HERE";
            if (env.Find(name) != null) return;
            CreateGoalVisual(env, side);
        }

        [MenuItem("Beast Soccer/Build Complete Prototype Scenes")]
        public static void BuildAll()
        {
            if(!AssetDatabase.IsValidFolder("Assets/Scenes"))AssetDatabase.CreateFolder("Assets","Scenes");
            // FIX18: prepare any Leo run PNGs/clips already present in the user project.
            // This is non-destructive: existing .anim clips are reused and never recreated.
            SpriteArtTools.PrepareLeoRunTestAssets();
            SpriteArtTools.PrepareLeoTempAnimationAssets();
            SpriteArtTools.PrepareVoltRunAssets();
            SpriteArtTools.PrepareUIAndPitchAssets();
            BuildMatch();
            BuildMenu();
            EnsureBuildSettings();
            EditorSceneManager.OpenScene($"{ScenesPath}/MainMenu.unity");
            Debug.Log("Beast Soccer scenes rebuilt. FIX26 uses ball-follow camera, locked crosses/throw-ins, calmer AI wide/through play, menu cleanup, kickoff idle sway, and smoother directional angle switching.");
        }

        private static void BuildMatch()
        {
            var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            var systems=new GameObject("SYSTEMS");
            systems.AddComponent<GameConfig>();
            systems.AddComponent<GameManager>();
            var artDb=systems.AddComponent<CharacterArtDatabase>();
            artDb.leoController = BuildLeoDirectionalTestControllerIfAvailable();
            artDb.voltController = BuildVoltDirectionalControllerIfAvailable();
            var tm=systems.AddComponent<TeamManager>();
            systems.AddComponent<MatchTimer>();
            systems.AddComponent<ScoreManager>();
            var reset=systems.AddComponent<RoundReset>();
            systems.AddComponent<ControlSwitcher>();
            systems.AddComponent<AudioManager>();
            systems.AddComponent<RuntimeSanityCheck>();

            var sim=new GameObject("SIMULATION_2D");
            var ball=CreateBall(sim.transform);
            tm.Ball=ball.transform;

            // Explicit 3+1: three outfield tactical roles plus one dedicated goalkeeper.
            var homeSpecial=CreatePlayer(sim.transform,"Home_ATT_Special",TeamSide.Home,FieldRole.Outfield,TacticalRole.Rover,CharacterType.Leo,10,new Vector2(-4f,2.2f));
            var homeGenericA=CreatePlayer(sim.transform,"Home_MID",TeamSide.Home,FieldRole.Outfield,TacticalRole.Presser,CharacterType.Generic,6,new Vector2(-3.8f,-2.2f));
            var homeGenericB=CreatePlayer(sim.transform,"Home_CB",TeamSide.Home,FieldRole.Outfield,TacticalRole.Anchor,CharacterType.Generic,8,new Vector2(-6.4f,0f));
            var homeGK=CreatePlayer(sim.transform,"Home_GK",TeamSide.Home,FieldRole.Goalkeeper,TacticalRole.Goalkeeper,CharacterType.Generic,1,new Vector2(-11.1f,0f));

            var awaySpecial=CreatePlayer(sim.transform,"Away_MID_Special",TeamSide.Away,FieldRole.Outfield,TacticalRole.Presser,CharacterType.Goro,10,new Vector2(3.8f,2.2f));
            var awayGenericA=CreatePlayer(sim.transform,"Away_ATT",TeamSide.Away,FieldRole.Outfield,TacticalRole.Rover,CharacterType.Generic,7,new Vector2(4f,-2.2f));
            var awayGenericB=CreatePlayer(sim.transform,"Away_CB",TeamSide.Away,FieldRole.Outfield,TacticalRole.Anchor,CharacterType.Generic,9,new Vector2(6.4f,0f));
            var awayGK=CreatePlayer(sim.transform,"Away_GK",TeamSide.Away,FieldRole.Goalkeeper,TacticalRole.Goalkeeper,CharacterType.Generic,1,new Vector2(11.1f,0f));

            tm.HomeTeam.AddRange(new[]{homeSpecial,homeGenericA,homeGenericB,homeGK});
            tm.AwayTeam.AddRange(new[]{awaySpecial,awayGenericA,awayGenericB,awayGK});

            var spawnRoot=new GameObject("SpawnPoints").transform;
            reset.ballSpawn=Spawn(spawnRoot,"BallSpawn",Vector2.zero);
            reset.homeSpawns=new[]{Spawn(spawnRoot,"HomeATTSpawn",new Vector2(-4f,2.2f)),Spawn(spawnRoot,"HomeMIDSpawn",new Vector2(-3.8f,-2.2f)),Spawn(spawnRoot,"HomeCBSpawn",new Vector2(-6.4f,0f)),Spawn(spawnRoot,"HomeGKSpawn",new Vector2(-11.1f,0f))};
            reset.awaySpawns=new[]{Spawn(spawnRoot,"AwayATTSpawn",new Vector2(4f,-2.2f)),Spawn(spawnRoot,"AwayMIDSpawn",new Vector2(3.8f,2.2f)),Spawn(spawnRoot,"AwayCBSpawn",new Vector2(6.4f,0f)),Spawn(spawnRoot,"AwayGKSpawn",new Vector2(11.1f,0f))};

            CreatePhysicsBoundaries(sim.transform);
            CreatePresentation(ball,new[]{homeSpecial,homeGenericA,homeGenericB,homeGK,awaySpecial,awayGenericA,awayGenericB,awayGK},artDb);
            CreateCamera();
            CreateMatchUI(new[]{homeSpecial,homeGenericA,homeGenericB,homeGK,awaySpecial,awayGenericA,awayGenericB,awayGK},ball.transform);
            CreateEventSystem();

            EditorSceneManager.SaveScene(scene,$"{ScenesPath}/Match.unity");
        }

        private static PlayerController CreatePlayer(Transform root,string name,TeamSide side,FieldRole role,TacticalRole tacticalRole,CharacterType type,int number,Vector2 pos)
        {
            var go=new GameObject(name);go.transform.SetParent(root);go.transform.position=pos;
            var rb=go.AddComponent<Rigidbody2D>();
            rb.gravityScale=0;rb.freezeRotation=true;rb.collisionDetectionMode=CollisionDetectionMode2D.Continuous;rb.interpolation=RigidbodyInterpolation2D.Interpolate;rb.linearDamping=2f;
            var cap=go.AddComponent<CapsuleCollider2D>();cap.size=new Vector2(.576f,.84f);cap.direction=CapsuleDirection2D.Vertical;cap.sharedMaterial=GetOrCreatePlayerPhysicsMaterial();
            var p=go.AddComponent<PlayerController>();p.Side=side;p.Role=role;p.FormationRole=tacticalRole;p.Character=type;p.ShirtNumber=number;p.IsHuman=false;
            go.AddComponent<DefensiveActions>();
            var ult=go.AddComponent<UltimateAbility>();
            var wingGO=new GameObject("WingBlockZone");wingGO.transform.SetParent(go.transform);wingGO.transform.localPosition=Vector3.zero;
            var wing=wingGO.AddComponent<BoxCollider2D>();wing.isTrigger=true;wing.size=new Vector2(.55f,1f);wing.enabled=false;
            var wingZone=wingGO.AddComponent<WingBlockZone>();wingZone.owner=p;wingZone.ability=ult;ult.wingBlockCollider=wing;ult.wingBlockZone=wingZone;
            go.AddComponent<AIController>();
            // No AIUltUsage component is added in the stabilized prototype; ults are human-triggered only.
            if(role==FieldRole.Outfield)go.AddComponent<DribbleRewardTracker>();
            // Every player keeps a dormant save zone so the chosen special can become GK in Defending mode.
            var z=new GameObject("SaveZone");z.transform.SetParent(go.transform);z.transform.localPosition=Vector3.zero;
            var cc=z.AddComponent<CircleCollider2D>();cc.isTrigger=true;cc.radius=1.15f;
            var sz=z.AddComponent<GoalkeeperSaveZone>();sz.keeper=p;
            return p;
        }


        private static PhysicsMaterial2D GetOrCreatePlayerPhysicsMaterial()
        {
            if(!AssetDatabase.IsValidFolder("Assets/Generated"))AssetDatabase.CreateFolder("Assets","Generated");
            const string path="Assets/Generated/BeastPlayer_NoGrip.physicsMaterial2D";
            var existing=AssetDatabase.LoadAssetAtPath<PhysicsMaterial2D>(path);
            if(existing!=null)return existing;
            var mat=new PhysicsMaterial2D("BeastPlayer_NoGrip"){friction=0f,bounciness=0f};
            AssetDatabase.CreateAsset(mat,path);
            return mat;
        }

        private static PhysicsMaterial2D GetOrCreateBallPostPhysicsMaterial()
        {
            if(!AssetDatabase.IsValidFolder("Assets/Generated"))AssetDatabase.CreateFolder("Assets","Generated");
            const string path="Assets/Generated/BeastBall_PostBounce.physicsMaterial2D";
            var existing=AssetDatabase.LoadAssetAtPath<PhysicsMaterial2D>(path);
            if(existing!=null)
            {
                existing.friction=0.02f;
                existing.bounciness=.78f;
                EditorUtility.SetDirty(existing);
                return existing;
            }
            var mat=new PhysicsMaterial2D("BeastBall_PostBounce"){friction=.02f,bounciness=.78f};
            AssetDatabase.CreateAsset(mat,path);
            return mat;
        }

        private static BallControl CreateBall(Transform root)
        {
            var go=new GameObject("Ball_SIM");go.transform.SetParent(root);
            var rb=go.AddComponent<Rigidbody2D>();rb.gravityScale=0;rb.collisionDetectionMode=CollisionDetectionMode2D.Continuous;rb.interpolation=RigidbodyInterpolation2D.Interpolate;
            var cc=go.AddComponent<CircleCollider2D>();cc.radius=.20f;cc.sharedMaterial=GetOrCreateBallPostPhysicsMaterial();
            go.AddComponent<BallPhysics>();
            var bc=go.AddComponent<BallControl>();
            go.AddComponent<BallCollision>();
            return bc;
        }

        private static Transform Spawn(Transform root,string name,Vector2 pos)
        {
            var g=new GameObject(name);g.transform.SetParent(root);g.transform.position=pos;return g.transform;
        }

        private static void CreatePhysicsBoundaries(Transform root)
        {
            float L=24f,W=15.86f;
            // Goal sensors live behind the line. GoalTrigger additionally verifies that the
            // complete ball has crossed the actual +/- L/2 goal line before awarding a goal.
            CreateGoal(root,"Goal_PosX",+1,L*.5f,new Vector2(L*.5f+.34f,0),new Vector2(.48f,4.4f));
            CreateGoal(root,"Goal_NegX",-1,-L*.5f,new Vector2(-L*.5f-.34f,0),new Vector2(.48f,4.4f));
            CreatePost(root,new Vector2(L*.5f,2.08f));CreatePost(root,new Vector2(L*.5f,-2.08f));
            CreatePost(root,new Vector2(-L*.5f,2.08f));CreatePost(root,new Vector2(-L*.5f,-2.08f));
            CreateBoundary(root,"Out_Top",new Vector2(0,W*.5f+.25f),new Vector2(L+1.2f,.38f),BoundaryTrigger.BoundaryKind.Touchline,0,+1);
            CreateBoundary(root,"Out_Bottom",new Vector2(0,-W*.5f-.25f),new Vector2(L+1.2f,.38f),BoundaryTrigger.BoundaryKind.Touchline,0,-1);
            CreateBoundary(root,"Out_PosX",new Vector2(L*.5f+.55f,0),new Vector2(.38f,W+1.2f),BoundaryTrigger.BoundaryKind.GoalLine,+1,0);
            CreateBoundary(root,"Out_NegX",new Vector2(-L*.5f-.55f,0),new Vector2(.38f,W+1.2f),BoundaryTrigger.BoundaryKind.GoalLine,-1,0);
        }


        private static void CreatePost(Transform root,Vector2 pos)
        {
            var g=new GameObject("GoalPost_SIM");g.transform.SetParent(root);g.transform.position=pos;
            var c=g.AddComponent<CircleCollider2D>();c.radius=.16f;c.sharedMaterial=GetOrCreateBallPostPhysicsMaterial();
        }

        private static void CreateGoal(Transform root,string name,int side,float goalLineX,Vector2 pos,Vector2 size)
        {
            var g=new GameObject(name);g.transform.SetParent(root);g.transform.position=pos;
            var c=g.AddComponent<BoxCollider2D>();c.isTrigger=true;c.size=size;
            var gt=g.AddComponent<GoalTrigger>();
            gt.worldGoalSide=side;
            gt.goalLineX=goalLineX;
            gt.goalHalfWidth=2.08f;
            gt.ballRadius=.20f;
        }
        private static void CreateBoundary(Transform root,string name,Vector2 pos,Vector2 size,BoundaryTrigger.BoundaryKind kind,int worldGoalSide,int touchlineSide)
        {
            var g=new GameObject(name);g.transform.SetParent(root);g.transform.position=pos;
            var c=g.AddComponent<BoxCollider2D>();c.isTrigger=true;c.size=size;
            var bt=g.AddComponent<BoundaryTrigger>();bt.kind=kind;bt.worldGoalSide=worldGoalSide;bt.touchlineSide=touchlineSide;
        }

        private static RuntimeAnimatorController BuildLeoDirectionalTestControllerIfAvailable()
        {
            // User-created clips from the earlier art test. We only READ these; never recreate them.
            AnimationClip side = FindAnimationClip("Leo_Run_Side");
            AnimationClip front3Q = FindAnimationClip("Leo_Run_3Q");
            if(front3Q==null) front3Q = FindAnimationClip("Leo_Run_Front3Q");
            if(side==null) return null;

            // FIX19-generated clips built from the new PNG folders.
            AnimationClip front = FindAnimationClip("Leo_Run_Front");
            AnimationClip shoot = FindAnimationClip("Leo_Shoot_Temp");
            AnimationClip ult = FindAnimationClip("Leo_Ult_Temp");

            if(!AssetDatabase.IsValidFolder("Assets/Generated")) AssetDatabase.CreateFolder("Assets","Generated");
            const string controllerPath="Assets/Generated/Leo_Directional_Test.controller";
            var controller=AssetDatabase.LoadAssetAtPath<AnimatorController>(controllerPath);
            if(controller==null) controller=AnimatorController.CreateAnimatorControllerAtPath(controllerPath);

            var sm=controller.layers[0].stateMachine;
            foreach(var child in sm.states) sm.RemoveState(child.state);

            var sideState=sm.AddState("Leo_Run_Side"); sideState.motion=side; sm.defaultState=sideState;
            if(front3Q!=null){var state=sm.AddState("Leo_Run_3Q"); state.motion=front3Q;}
            if(front!=null){var state=sm.AddState("Leo_Run_Front"); state.motion=front;}
            if(shoot!=null){var state=sm.AddState("Leo_Shoot_Temp"); state.motion=shoot;}
            if(ult!=null){var state=sm.AddState("Leo_Ult_Temp"); state.motion=ult;}

            EditorUtility.SetDirty(controller);
            AssetDatabase.SaveAssets();
            Debug.Log($"[Beast Soccer] FIX19 Leo controller: Side={(side!=null)}, 3Q={(front3Q!=null)}, Front={(front!=null)}, Shoot={(shoot!=null)}, Ult={(ult!=null)}. Existing Side/3Q clips preserved.");
            return controller;
        }

        private static RuntimeAnimatorController BuildVoltDirectionalControllerIfAvailable()
        {
            AnimationClip side=FindAnimationClip("Volt_Run_Side");
            AnimationClip front3Q=FindAnimationClip("Volt_Run_Front3Q");
            AnimationClip front=FindAnimationClip("Volt_Run_Front");
            AnimationClip back3Q=FindAnimationClip("Volt_Run_Back3Q");
            AnimationClip back=FindAnimationClip("Volt_Run_Back");
            if(side==null) return null;

            if(!AssetDatabase.IsValidFolder("Assets/Generated"))AssetDatabase.CreateFolder("Assets","Generated");
            const string controllerPath="Assets/Generated/Volt_Directional_Run.controller";
            var controller=AssetDatabase.LoadAssetAtPath<AnimatorController>(controllerPath);
            if(controller==null) controller=AnimatorController.CreateAnimatorControllerAtPath(controllerPath);
            var sm=controller.layers[0].stateMachine;
            foreach(var child in sm.states) sm.RemoveState(child.state);
            var sideState=sm.AddState("Volt_Run_Side");sideState.motion=side;sm.defaultState=sideState;
            if(front3Q!=null){var st=sm.AddState("Volt_Run_Front3Q");st.motion=front3Q;}
            if(front!=null){var st=sm.AddState("Volt_Run_Front");st.motion=front;}
            if(back3Q!=null){var st=sm.AddState("Volt_Run_Back3Q");st.motion=back3Q;}
            if(back!=null){var st=sm.AddState("Volt_Run_Back");st.motion=back;}
            EditorUtility.SetDirty(controller);AssetDatabase.SaveAssets();
            Debug.Log($"[Beast Soccer] Volt directional controller: Side={side!=null}, Front3Q={front3Q!=null}, Front={front!=null}, Back3Q={back3Q!=null}, Back={back!=null}.");
            return controller;
        }

        private static AnimationClip FindAnimationClip(string exactName)
        {
            foreach(string guid in AssetDatabase.FindAssets(exactName+" t:AnimationClip"))
            {
                string path=AssetDatabase.GUIDToAssetPath(guid);
                var clip=AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
                if(clip!=null && clip.name==exactName) return clip;
            }
            return null;
        }

        private static void CreatePresentation(BallControl ball,PlayerController[] players,CharacterArtDatabase artDb)
        {
            var root=new GameObject("VISUALS_3D").transform;
            var env=new GameObject("Environment_ATTACH_OR_REPLACE_3D_MODELS").transform;env.SetParent(root);
            var pitch=GameObject.CreatePrimitive(PrimitiveType.Plane);pitch.name="Pitch_Artwork";pitch.transform.SetParent(env);
            pitch.transform.localScale=new Vector3(2.4f,1f,1.586f);
            pitch.GetComponent<Renderer>().sharedMaterial=MakePitchArtworkMaterial();
            UnityEngine.Object.DestroyImmediate(pitch.GetComponent<Collider>());
            // Pitch markings live in the supplied artwork. Invisible simulation goal/post/boundary
            // colliders remain under SIMULATION_2D. Visual goal roots are generated as explicit
            // prefab drop-points so real goal models can be replaced without touching gameplay.
            CreateGoalVisual(env, +1);
            CreateGoalVisual(env, -1);

            var ballProxy=new GameObject("Ball_3D_ATTACH_MODEL");ballProxy.transform.SetParent(root);
            var sphere=GameObject.CreatePrimitive(PrimitiveType.Sphere);sphere.name="BallModel_PLACEHOLDER";sphere.transform.SetParent(ballProxy.transform);sphere.transform.localPosition=Vector3.zero;sphere.transform.localScale=Vector3.one*.36f;UnityEngine.Object.DestroyImmediate(sphere.GetComponent<Collider>());
            var bp=ballProxy.AddComponent<BallVisualProxy>();bp.source=ball;bp.visualModel=sphere.transform;

            var pr=new GameObject("Players_ADD_SPRITES_HERE").transform;pr.SetParent(root);
            foreach(var p in players)
            {
                var v=new GameObject(p.name+"_Visual");v.transform.SetParent(pr);v.transform.localScale=Vector3.one*0.936f;
                var sr=v.AddComponent<SpriteRenderer>();sr.sortingOrder=10;
                var animator=v.AddComponent<Animator>();
                var driver=v.AddComponent<CharacterAnimationDriver>();driver.animator=animator;
                var library=v.AddComponent<CharacterVisualLibrary>();library.database=artDb;
                var proxy=v.AddComponent<PlayerVisualProxy>();proxy.source=p;proxy.spriteRenderer=sr;proxy.animator=animator;proxy.animationDriver=driver;proxy.visualLibrary=library;proxy.baseHeight=.045f;
                p.Visual=proxy;p.Animation=driver;

                var trail=v.AddComponent<TrailRenderer>();trail.time=.48f;trail.startWidth=.18f;trail.endWidth=.025f;trail.minVertexDistance=.08f;trail.material=MakeMaterial(new Color(.20f,.82f,1f,.72f));trail.enabled=false;proxy.voltFlightTrail=trail;

                var shadow=GameObject.CreatePrimitive(PrimitiveType.Cylinder);shadow.name=p.name+"_Shadow";shadow.transform.localScale=new Vector3(.36f,.01f,.25f);shadow.GetComponent<Renderer>().sharedMaterial=MakeMaterial(new Color(0,0,0,.28f));UnityEngine.Object.DestroyImmediate(shadow.GetComponent<Collider>());shadow.transform.SetParent(root);proxy.shadow=shadow.transform;

                // FIX18: use an outline ring instead of a filled cylinder. The old debug disk
                // could depth-occlude real sprite art from the angled camera.
                var selected=new GameObject(p.name+"_CONTROLLED");selected.transform.SetParent(root);
                var selectedLine=selected.AddComponent<LineRenderer>();selectedLine.useWorldSpace=false;selectedLine.loop=true;selectedLine.positionCount=48;selectedLine.widthMultiplier=.055f;selectedLine.numCornerVertices=2;selectedLine.numCapVertices=2;selectedLine.sharedMaterial=MakeMaterial(new Color(1f,.86f,.12f,.95f));
                for(int ri=0;ri<48;ri++){float a=(ri/48f)*Mathf.PI*2f;selectedLine.SetPosition(ri,new Vector3(Mathf.Cos(a)*.48f,0f,Mathf.Sin(a)*.48f));}
                selected.SetActive(false);proxy.controlledIndicator=selected;

                var ultAtk=GameObject.CreatePrimitive(PrimitiveType.Cylinder);ultAtk.name=p.name+"_ULT_ATTACK";ultAtk.transform.SetParent(root);ultAtk.transform.localScale=new Vector3(.64f,.010f,.64f);ultAtk.GetComponent<Renderer>().sharedMaterial=MakeMaterial(new Color(1f,.35f,.08f,.72f));UnityEngine.Object.DestroyImmediate(ultAtk.GetComponent<Collider>());ultAtk.SetActive(false);proxy.ultAttackIndicator=ultAtk;
                var ultDef=GameObject.CreatePrimitive(PrimitiveType.Cylinder);ultDef.name=p.name+"_ULT_DEFENCE";ultDef.transform.SetParent(root);ultDef.transform.localScale=new Vector3(.64f,.010f,.64f);ultDef.GetComponent<Renderer>().sharedMaterial=MakeMaterial(new Color(.08f,.75f,1f,.72f));UnityEngine.Object.DestroyImmediate(ultDef.GetComponent<Collider>());ultDef.SetActive(false);proxy.ultDefenseIndicator=ultDef;

                var possession=new GameObject(p.name+"_POSSESSION");possession.transform.SetParent(root);possession.transform.rotation=Quaternion.Euler(90f,0f,0f);
                var possessionSR=possession.AddComponent<SpriteRenderer>();possessionSR.sprite=LoadSprite("Assets/Art/UI/Possession_Control.png");possessionSR.sortingOrder=4;
                possession.transform.localScale=Vector3.one*.22f;possession.SetActive(false);proxy.possessionIndicator=possession;

                var facing=GameObject.CreatePrimitive(PrimitiveType.Cube);facing.name=p.name+"_FACING";facing.transform.SetParent(root);facing.transform.localScale=new Vector3(.07f,.025f,.34f);facing.GetComponent<Renderer>().sharedMaterial=MakeMaterial(new Color(1f,1f,1f,.75f));UnityEngine.Object.DestroyImmediate(facing.GetComponent<Collider>());proxy.facingIndicator=facing.transform;

                var labelGO=new GameObject(p.name+"_NAME_POSITION");labelGO.transform.SetParent(root);
                var label=labelGO.AddComponent<TextMesh>();label.text="PLAYER  ○ - POSITION";label.fontSize=42;label.characterSize=.050f;label.anchor=TextAnchor.MiddleCenter;label.alignment=TextAlignment.Center;label.color=Color.white;proxy.roleLabel=label;

                // Temporary flight-height guide. It is only visible while Volt is airborne and
                // makes the separation between sprite and ground shadow unmistakable in prototype art.
                var flight=GameObject.CreatePrimitive(PrimitiveType.Cylinder);flight.name=p.name+"_FLIGHT_HEIGHT";flight.transform.SetParent(root);flight.transform.localScale=new Vector3(.055f,.7f,.055f);flight.GetComponent<Renderer>().sharedMaterial=MakeMaterial(new Color(.30f,.85f,1f,.42f));UnityEngine.Object.DestroyImmediate(flight.GetComponent<Collider>());flight.SetActive(false);proxy.flightIndicator=flight;

                var marker=GameObject.CreatePrimitive(PrimitiveType.Capsule);marker.name="DELETE_WHEN_SPRITE_ADDED";marker.transform.SetParent(v.transform);marker.transform.localPosition=new Vector3(0,-.25f,0);marker.transform.localScale=new Vector3(.45f,.7f,.22f);UnityEngine.Object.DestroyImmediate(marker.GetComponent<Collider>());
                Color markerColor;
                if (p.Role == FieldRole.Goalkeeper) markerColor = p.Side == TeamSide.Home ? new Color(.95f,.78f,.12f) : new Color(1f,.52f,.12f);
                else markerColor = p.Side == TeamSide.Home ? new Color(.15f,.38f,.95f) : new Color(.9f,.15f,.2f);
                marker.GetComponent<Renderer>().sharedMaterial=MakeMaterial(markerColor);library.placeholderToDisable=marker;
            }
        }


        private static void CreatePitchDebugMarkings(Transform root)
        {
            var mat=MakeMaterial(new Color(1f,1f,1f,.72f));
            void Line(string name,Vector3 pos,Vector3 scale)
            {
                var g=GameObject.CreatePrimitive(PrimitiveType.Cube);g.name=name;g.transform.SetParent(root);g.transform.position=pos;g.transform.localScale=scale;g.GetComponent<Renderer>().sharedMaterial=mat;UnityEngine.Object.DestroyImmediate(g.GetComponent<Collider>());
            }
            float y=.025f;
            Line("HalfwayLine",new Vector3(0,y,0),new Vector3(.045f,.012f,14f));
            Line("TouchlineTop",new Vector3(0,y,7f),new Vector3(24f,.012f,.045f));
            Line("TouchlineBottom",new Vector3(0,y,-7f),new Vector3(24f,.012f,.045f));
            Line("GoalLinePos",new Vector3(12f,y,0),new Vector3(.045f,.012f,14f));
            Line("GoalLineNeg",new Vector3(-12f,y,0),new Vector3(.045f,.012f,14f));
            foreach(int side in new[]{-1,1})
            {
                float gx=side*12f;float boxX=gx-side*3.0f;
                Line("BoxFront_"+side,new Vector3(boxX,y,0),new Vector3(.045f,.012f,6.2f));
                Line("BoxTop_"+side,new Vector3((gx+boxX)*.5f,y,3.1f),new Vector3(3.0f,.012f,.045f));
                Line("BoxBottom_"+side,new Vector3((gx+boxX)*.5f,y,-3.1f),new Vector3(3.0f,.012f,.045f));
            }
            for(int i=0;i<20;i++)
            {
                float a=i*Mathf.PI*2f/20f;
                var g=GameObject.CreatePrimitive(PrimitiveType.Cube);g.name="CenterCircle_"+i;g.transform.SetParent(root);g.transform.position=new Vector3(Mathf.Cos(a)*1.5f,y,Mathf.Sin(a)*1.5f);g.transform.rotation=Quaternion.Euler(0f,-a*Mathf.Rad2Deg,0f);g.transform.localScale=new Vector3(.04f,.012f,.48f);g.GetComponent<Renderer>().sharedMaterial=mat;UnityEngine.Object.DestroyImmediate(g.GetComponent<Collider>());
            }
        }

        private static void CreateGoalVisual(Transform root,int side)
        {
            float x=side*12f;float w=3.7f;float h=1.8f;
            var a=new GameObject(side>0?"Goal_PosX_ATTACH_PREFAB_HERE":"Goal_NegX_ATTACH_PREFAB_HERE").transform;a.SetParent(root);a.position=new Vector3(x,0,0);
            CreateBar(a,"PLACEHOLDER_PostA",new Vector3(0,h*.5f,-w*.5f),new Vector3(.12f,h,.12f));
            CreateBar(a,"PLACEHOLDER_PostB",new Vector3(0,h*.5f,w*.5f),new Vector3(.12f,h,.12f));
            CreateBar(a,"PLACEHOLDER_Crossbar",new Vector3(0,h,0),new Vector3(.12f,.12f,w));
        }
        private static void CreateBar(Transform parent,string name,Vector3 lp,Vector3 scale)
        {
            var g=GameObject.CreatePrimitive(PrimitiveType.Cube);g.name=name;g.transform.SetParent(parent);g.transform.localPosition=lp;g.transform.localScale=scale;g.GetComponent<Renderer>().sharedMaterial=MakeMaterial(Color.white);UnityEngine.Object.DestroyImmediate(g.GetComponent<Collider>());
        }
        private static Sprite LoadSprite(string path)
        {
            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }

        private static Material MakePitchArtworkMaterial()
        {
            if(!AssetDatabase.IsValidFolder("Assets/Generated"))AssetDatabase.CreateFolder("Assets","Generated");
            const string path="Assets/Generated/BeastPitchArtwork.mat";
            var tex=AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Art/Pitch/Pitch_Cropped.jpg");
            var m=AssetDatabase.LoadAssetAtPath<Material>(path);
            if(m==null)
            {
                Shader shader=Shader.Find("Universal Render Pipeline/Unlit");
                if(shader==null) shader=Shader.Find("Unlit/Texture");
                if(shader==null) shader=Shader.Find("Standard");
                m=new Material(shader);AssetDatabase.CreateAsset(m,path);
            }
            if(tex!=null) m.mainTexture=tex;
            m.color=Color.white;
            EditorUtility.SetDirty(m);
            return m;
        }

        private static Material MakeMaterial(Color c)
        {
            if(!AssetDatabase.IsValidFolder("Assets/Generated"))AssetDatabase.CreateFolder("Assets","Generated");
            string hex=ColorUtility.ToHtmlStringRGBA(c);
            string path=$"Assets/Generated/BeastMat_{hex}.mat";
            var existing=AssetDatabase.LoadAssetAtPath<Material>(path);
            if(existing!=null)return existing;
            Shader shader=Shader.Find("Universal Render Pipeline/Lit");
            if(shader==null)shader=Shader.Find("Standard");
            if(shader==null)shader=Shader.Find("Sprites/Default");
            var m=new Material(shader);m.color=c;AssetDatabase.CreateAsset(m,path);return m;
        }

        private static void CreateCamera()
        {
            var go=new GameObject("Main Camera");go.tag="MainCamera";
            var cam=go.AddComponent<Camera>();cam.clearFlags=CameraClearFlags.SolidColor;cam.backgroundColor=new Color(.08f,.12f,.22f);cam.fieldOfView=46f;cam.transparencySortMode=TransparencySortMode.CustomAxis;cam.transparencySortAxis=new Vector3(0f,0f,1f);
            go.AddComponent<AudioListener>();go.AddComponent<FollowCamera>();
            go.transform.position=new Vector3(-4f,10f,-8f);go.transform.rotation=Quaternion.Euler(50f,0,0);
            var light=new GameObject("Directional Light");var dl=light.AddComponent<Light>();dl.type=LightType.Directional;dl.intensity=1.1f;light.transform.rotation=Quaternion.Euler(55f,-35f,0);
        }

        private static void CreateMatchUI(PlayerController[] players,Transform ball)
        {
            var canvasGO=new GameObject("Canvas");var canvas=canvasGO.AddComponent<Canvas>();canvas.renderMode=RenderMode.ScreenSpaceOverlay;canvasGO.AddComponent<CanvasScaler>().uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;canvasGO.GetComponent<CanvasScaler>().referenceResolution=new Vector2(1920,1080);canvasGO.AddComponent<GraphicRaycaster>();
            var ui=canvasGO.AddComponent<MatchUI>();
            var input=canvasGO.AddComponent<TouchInputManager>();

            ui.ultGoalText=TextEl(canvasGO.transform,"UltGoalBanner","ULT GOAL!",new Vector2(0,250),54,TextAnchor.MiddleCenter,new Vector2(900,90));ui.ultGoalText.gameObject.SetActive(false);

            var topHud=Panel(canvasGO.transform,"TopHUD_Backdrop",new Vector2(0,466),new Vector2(350,118),new Color(.025f,.04f,.07f,.76f));
            topHud.GetComponent<Image>().raycastTarget=false;
            ui.scoreText=TextEl(canvasGO.transform,"Score","0 - 0",new Vector2(0,490),34,TextAnchor.MiddleCenter,new Vector2(240,55));
            ui.timerText=TextEl(canvasGO.transform,"Timer","00:00",new Vector2(0,445),29,TextAnchor.MiddleCenter,new Vector2(240,45));
            ui.bannerText=TextEl(canvasGO.transform,"Banner","",new Vector2(0,350),46,TextAnchor.MiddleCenter,new Vector2(700,80));

            var mini=Panel(canvasGO.transform,"Minimap",new Vector2(-760,400),new Vector2(300,180),new Color(0,0,0,.55f));
            var mm=mini.AddComponent<MinimapUI>();mm.mapArea=mini.GetComponent<RectTransform>();
            mm.dots=new MinimapUI.TrackedDot[players.Length];
            for(int i=0;i<players.Length;i++)
            {
                Color c=players[i].Role==FieldRole.Goalkeeper?(players[i].Side==TeamSide.Home?new Color(.95f,.78f,.12f):new Color(1f,.52f,.12f)):(players[i].Side==TeamSide.Home?Color.cyan:Color.red);
                var dot=Panel(mini.transform,"P"+i,Vector2.zero,new Vector2(16,16),c).GetComponent<RectTransform>();
                mm.dots[i]=new MinimapUI.TrackedDot{target=players[i].transform,dot=dot};
            }
            mm.ballDot=Panel(mini.transform,"BallDot",Vector2.zero,new Vector2(12,12),Color.white).GetComponent<RectTransform>();

            // Supplied movement pad artwork. The grey WALK ring / green SPRINT ring sit underneath
            // the direction pad. The outer direction plate stays fixed while only the centre knob moves.
            var joy=Panel(canvasGO.transform,"Joystick",new Vector2(-690,-315),new Vector2(370,370),new Color(1f,1f,1f,0f));
            var joyImage=joy.GetComponent<Image>();joyImage.raycastTarget=true;
            var walkRing=Panel(joy.transform,"WalkRing",Vector2.zero,new Vector2(390,390),Color.white);
            var walkImage=walkRing.GetComponent<Image>();walkImage.sprite=LoadSprite("Assets/Art/UI/Joystick_Walk.png");walkImage.preserveAspect=true;walkImage.raycastTarget=false;
            var sprintRing=Panel(joy.transform,"SprintRing",Vector2.zero,new Vector2(390,390),Color.white);
            var sprintImage=sprintRing.GetComponent<Image>();sprintImage.sprite=LoadSprite("Assets/Art/UI/Joystick_Sprint.png");sprintImage.preserveAspect=true;sprintImage.raycastTarget=false;sprintImage.enabled=false;
            var directionOuter=Panel(joy.transform,"DirectionPadOuter",Vector2.zero,new Vector2(300,300),Color.white);
            var directionOuterImage=directionOuter.GetComponent<Image>();directionOuterImage.sprite=LoadSprite("Assets/Art/UI/Joystick_Outer.png");directionOuterImage.preserveAspect=true;directionOuterImage.raycastTarget=false;
            var knob=Panel(joy.transform,"DirectionKnob",Vector2.zero,new Vector2(112,112),Color.white);
            var knobImage=knob.GetComponent<Image>();knobImage.sprite=LoadSprite("Assets/Art/UI/Joystick_Knob.png");knobImage.preserveAspect=true;knobImage.raycastTarget=false;
            var js=joy.AddComponent<JoystickInput>();js.background=joy.GetComponent<RectTransform>();js.knob=knob.GetComponent<RectTransform>();js.walkRing=walkImage;js.sprintRing=sprintImage;js.knobRange=108f;input.joystick=js;

            // Three stable touch targets. MatchUI swaps supplied icons/labels without destroying the
            // buttons, so an in-progress touch is not lost when possession changes.
            ui.primaryButton=ButtonEl(canvasGO.transform,"ActionPrimary",new Vector2(790,-245),new Vector2(190,190),Color.white,input.OnPrimary,32);
            ui.secondaryButton=ButtonEl(canvasGO.transform,"ActionSecondary",new Vector2(595,-435),new Vector2(168,168),Color.white,input.OnSecondary,30);
            ui.tertiaryButton=ButtonEl(canvasGO.transform,"ActionTertiary",new Vector2(610,-285),new Vector2(154,154),Color.white,input.OnTertiary,29);
            ui.primaryLabel=ui.primaryButton.GetComponentInChildren<Text>();
            ui.secondaryLabel=ui.secondaryButton.GetComponentInChildren<Text>();
            ui.tertiaryLabel=ui.tertiaryButton.GetComponentInChildren<Text>();
            ui.shootButtonSprite=LoadSprite("Assets/Art/UI/Shoot.png");
            ui.passButtonSprite=LoadSprite("Assets/Art/UI/Pass.png");
            ui.lobButtonSprite=LoadSprite("Assets/Art/UI/Lob.png");
            ui.ultButtonSprite=LoadSprite("Assets/Art/UI/Ult.png");

            var ult=ButtonEl(canvasGO.transform,"ULT",new Vector2(795,-440),new Vector2(188,188),Color.white,input.OnUltimate,30);
            ui.ultimateButton=ult;ui.ultimateLabel=ult.GetComponentInChildren<Text>();ui.ultButtonImage=ult.GetComponent<Image>();
            if(ui.ultButtonSprite!=null){ui.ultButtonImage.sprite=ui.ultButtonSprite;ui.ultButtonImage.preserveAspect=true;}
            var fly=ButtonEl(canvasGO.transform,"SPECIAL_ACTION",new Vector2(600,-155),new Vector2(170,76),new Color(.18f,.32f,.40f,.62f),input.OnFly,28);
            ui.flyButton=fly;ui.flyLabel=fly.GetComponentInChildren<Text>();fly.gameObject.SetActive(false);
            // FIX21: LOB uses ActionTertiary; no duplicate standalone LOB button is generated.
            ui.lobButton=null;ui.lobLabel=null;
            var activeFillGO=Panel(ult.transform,"ActiveDrain",Vector2.zero,new Vector2(180,180),new Color(1f,.82f,.15f,.42f));
            activeFillGO.transform.SetAsFirstSibling();
            var activeFill=activeFillGO.GetComponent<Image>();activeFill.type=Image.Type.Filled;activeFill.fillMethod=Image.FillMethod.Radial360;activeFill.fillOrigin=(int)Image.Origin360.Top;activeFill.fillClockwise=false;activeFill.fillAmount=0f;activeFill.raycastTarget=false;ui.ultActiveFill=activeFill;

            var switchButton=ButtonEl(canvasGO.transform,"SWITCH",new Vector2(795,-95),new Vector2(170,86),new Color(.1f,.75f,.75f,.85f),input.OnSwitch,28);ui.switchButton=switchButton;
            ButtonEl(canvasGO.transform,"II",new Vector2(870,490),new Vector2(80,70),new Color(.15f,.2f,.3f,.8f),input.OnPause);

            var sprintStamina=Panel(canvasGO.transform,"SprintStamina",new Vector2(-700,-175),new Vector2(250,38),new Color(.05f,.08f,.12f,.88f));ui.sprintStaminaPanel=sprintStamina;
            var sprintFill=Panel(sprintStamina.transform,"Fill",new Vector2(-115,-9),new Vector2(230,10),new Color(.20f,.86f,.42f,.96f)).GetComponent<Image>();
            sprintFill.type=Image.Type.Simple;sprintFill.raycastTarget=false;var sprintRT=sprintFill.rectTransform;sprintRT.pivot=new Vector2(0f,.5f);ui.sprintStaminaFill=sprintFill;ui.sprintStaminaMaxWidth=230f;

            var ultMeter=Panel(canvasGO.transform,"UltMeter",new Vector2(795,-335),new Vector2(184,20),new Color(.15f,.15f,.2f,.8f)).GetComponent<Image>();
            var fill=Panel(ultMeter.transform,"Fill",Vector2.zero,new Vector2(184,20),new Color(.65f,.2f,1f,1f)).GetComponent<Image>();fill.type=Image.Type.Filled;fill.fillMethod=Image.FillMethod.Horizontal;fill.fillAmount=0;fill.raycastTarget=false;ui.ultMeterFill=fill;

            var ultStamina=Panel(canvasGO.transform,"UltStamina",new Vector2(0,385),new Vector2(520,54),new Color(.04f,.04f,.08f,.90f));ui.ultStaminaPanel=ultStamina;
            var staminaFill=Panel(ultStamina.transform,"StaminaFill",new Vector2(0,-14),new Vector2(500,14),new Color(1f,.72f,.10f,.95f)).GetComponent<Image>();staminaFill.type=Image.Type.Filled;staminaFill.fillMethod=Image.FillMethod.Horizontal;staminaFill.fillOrigin=(int)Image.OriginHorizontal.Left;staminaFill.fillAmount=1f;staminaFill.raycastTarget=false;ui.ultStaminaFill=staminaFill;
            ultStamina.SetActive(false);

            var halfPanel=Panel(canvasGO.transform,"HalfTimePanel",Vector2.zero,new Vector2(700,350),new Color(.03f,.05f,.1f,.94f));
            TextEl(halfPanel.transform,"HalfText","HALF TIME",new Vector2(0,70),52,TextAnchor.MiddleCenter,new Vector2(500,80));
            var htu=canvasGO.AddComponent<HalftimeUI>();htu.panel=halfPanel;
            ButtonEl(halfPanel.transform,"CONTINUE",new Vector2(0,-70),new Vector2(260,90),new Color(.15f,.55f,.95f,1f),htu.OnContinue);halfPanel.SetActive(false);

            var pausePanel=Panel(canvasGO.transform,"PausePanel",Vector2.zero,new Vector2(620,500),new Color(.03f,.05f,.1f,.94f));
            TextEl(pausePanel.transform,"PauseText","PAUSED",new Vector2(0,165),48,TextAnchor.MiddleCenter,new Vector2(400,80));
            var pu=canvasGO.AddComponent<PauseUI>();pu.panel=pausePanel;pu.mainMenuScene="MainMenu";
            ButtonEl(pausePanel.transform,"RESUME",new Vector2(0,55),new Vector2(280,82),new Color(.15f,.55f,.95f,1f),pu.OnResume,28);
            ButtonEl(pausePanel.transform,"RESTART MATCH",new Vector2(0,-55),new Vector2(280,82),new Color(.22f,.50f,.25f,1f),pu.OnRestart,27);
            ButtonEl(pausePanel.transform,"QUIT TO MENU",new Vector2(0,-165),new Vector2(280,82),new Color(.62f,.18f,.20f,1f),pu.OnQuitToMenu,27);
            pausePanel.SetActive(false);

            var fullPanel=Panel(canvasGO.transform,"FullTimePanel",Vector2.zero,new Vector2(720,590),new Color(.03f,.05f,.1f,.97f));
            TextEl(fullPanel.transform,"FullTimeText","FULL TIME",new Vector2(0,220),54,TextAnchor.MiddleCenter,new Vector2(520,80));
            var ftu=canvasGO.AddComponent<FullTimeUI>();ftu.panel=fullPanel;ftu.mainMenuScene="MainMenu";
            ftu.finalScoreText=TextEl(fullPanel.transform,"FinalScore","0  -  0",new Vector2(0,130),48,TextAnchor.MiddleCenter,new Vector2(420,70));
            ButtonEl(fullPanel.transform,"RESTART MATCH",new Vector2(0,25),new Vector2(320,82),new Color(.22f,.50f,.25f,1f),ftu.OnRestart,28);
            ButtonEl(fullPanel.transform,"CHANGE PLAYERS",new Vector2(0,-85),new Vector2(320,82),new Color(.15f,.55f,.95f,1f),ftu.OnChangePlayers,28);
            ButtonEl(fullPanel.transform,"MAIN MENU",new Vector2(0,-195),new Vector2(320,82),new Color(.62f,.18f,.20f,1f),ftu.OnMainMenu,28);
            fullPanel.SetActive(false);
        }

        private static void BuildMenu()
        {
            var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            var canvasGO=new GameObject("Canvas");var c=canvasGO.AddComponent<Canvas>();c.renderMode=RenderMode.ScreenSpaceOverlay;canvasGO.AddComponent<CanvasScaler>().referenceResolution=new Vector2(1920,1080);canvasGO.GetComponent<CanvasScaler>().uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;canvasGO.AddComponent<GraphicRaycaster>();
            var menu=canvasGO.AddComponent<MainMenuUI>();menu.matchScene="Match";
            TextEl(canvasGO.transform,"Title","BEAST SOCCER",new Vector2(0,360),64,TextAnchor.MiddleCenter,new Vector2(800,100));
            var mode=Panel(canvasGO.transform,"ModePanel",Vector2.zero,new Vector2(900,500),new Color(.04f,.08f,.16f,.95f));menu.modePanel=mode;
            ButtonEl(mode.transform,"REGULAR",new Vector2(-210,0),new Vector2(300,110),Color.blue,menu.OnSelectRegular);
            var defendingButton=ButtonEl(mode.transform,"DEFENDING",new Vector2(210,0),new Vector2(300,110),new Color(.22f,.22f,.26f,.72f),null);defendingButton.interactable=false;
            var chars=Panel(canvasGO.transform,"CharacterPanel",Vector2.zero,new Vector2(1000,500),new Color(.04f,.08f,.16f,.95f));menu.characterPanel=chars;
            ButtonEl(chars.transform,"VOLT",new Vector2(-320,0),new Vector2(250,110),new Color(.9f,.75f,.1f),menu.OnPickVolt);ButtonEl(chars.transform,"LEO",new Vector2(0,0),new Vector2(250,110),new Color(.85f,.3f,.12f),menu.OnPickLeo);ButtonEl(chars.transform,"GORO",new Vector2(320,0),new Vector2(250,110),new Color(.2f,.7f,.3f),menu.OnPickGoro);chars.SetActive(false);
            var opp=Panel(canvasGO.transform,"OpponentPanel",Vector2.zero,new Vector2(1100,500),new Color(.04f,.08f,.16f,.95f));menu.opponentPanel=opp;
            ButtonEl(opp.transform,"VOLT",new Vector2(-360,70),new Vector2(220,100),new Color(.9f,.75f,.1f),menu.OnOpponentVolt);ButtonEl(opp.transform,"LEO",new Vector2(-120,70),new Vector2(220,100),new Color(.85f,.3f,.12f),menu.OnOpponentLeo);ButtonEl(opp.transform,"GORO",new Vector2(120,70),new Vector2(220,100),new Color(.2f,.7f,.3f),menu.OnOpponentGoro);ButtonEl(opp.transform,"RANDOM",new Vector2(360,70),new Vector2(220,100),new Color(.35f,.35f,.5f),menu.OnOpponentRandom);opp.SetActive(false);
            CreateEventSystem();CreateCamera();
            EditorSceneManager.SaveScene(scene,$"{ScenesPath}/MainMenu.unity");
        }

        private static GameObject Panel(Transform parent,string name,Vector2 pos,Vector2 size,Color color)
        {
            var g=new GameObject(name);g.transform.SetParent(parent,false);var r=g.AddComponent<RectTransform>();r.sizeDelta=size;r.anchoredPosition=pos;var i=g.AddComponent<Image>();i.color=color;return g;
        }
        private static Text TextEl(Transform parent,string name,string text,Vector2 pos,int size,TextAnchor anchor,Vector2 dims)
        {
            var g=new GameObject(name);g.transform.SetParent(parent,false);var r=g.AddComponent<RectTransform>();r.sizeDelta=dims;r.anchoredPosition=pos;var t=g.AddComponent<Text>();t.font=Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");t.text=text;t.fontSize=size;t.alignment=anchor;t.color=Color.white;return t;
        }
        private static Button ButtonEl(Transform parent,string label,Vector2 pos,Vector2 size,Color color,UnityEngine.Events.UnityAction action,int fontSize=24)
        {
            var g=Panel(parent,label,pos,size,color);
            var b=g.AddComponent<Button>();
            b.targetGraphic=g.GetComponent<Image>();
            if(action!=null)UnityEventTools.AddPersistentListener(b.onClick,action);
            var text=TextEl(g.transform,"Text",label,Vector2.zero,fontSize,TextAnchor.MiddleCenter,size);
            text.resizeTextForBestFit=true;
            text.resizeTextMinSize=Mathf.Max(18,fontSize-8);
            text.resizeTextMaxSize=fontSize;
            return b;
        }
        private static void CreateEventSystem(){if(Object.FindAnyObjectByType<EventSystem>()!=null)return;var e=new GameObject("EventSystem");e.AddComponent<EventSystem>();e.AddComponent<StandaloneInputModule>();}
        private static void EnsureBuildSettings()
        {
            EditorBuildSettings.scenes=new[]{new EditorBuildSettingsScene($"{ScenesPath}/MainMenu.unity",true),new EditorBuildSettingsScene($"{ScenesPath}/Match.unity",true)};
        }
    }
}
#endif
