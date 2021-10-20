using System;
using System.Collections;
using System.Collections.Generic;
using PersonalLogistics.Logistics;
using PersonalLogistics.PlayerInventory;
using PersonalLogistics.Util;
using UnityEngine;

namespace PersonalLogistics.Scripts
{
    public class TraversalScript : MonoBehaviour
    {
        private bool loggedException = false;
        private OrderNode _lastOrder;
        private DateTime _lastOrderCreatedAt = DateTime.Now.Subtract(TimeSpan.FromDays(5));
        private Vector3Int _positionWhenLastOrderGiven = Vector3Int.zero;
        private static HashSet<int> _bpIds = new HashSet<int>();
        private static PlanetData _planet;
        private static PlanetFactory _factory;

        void Awake()
        {
            StartCoroutine(Loop());
        }


        IEnumerator Loop()
        {
            while (true)
            {
                yield return new WaitForSeconds(5);
                if (PluginConfig.followBluePrint.Value && LogisticsNetwork.IsInitted && LogisticsNetwork.IsFirstLoadComplete)
                {
                    FollowBluePrint();
                }
            }
        }

        private void FollowBluePrint()
        {
            if (GameMain.localPlanet == null || GameMain.localPlanet.factory == null) 
            {
                return;
            }
            // if (_bpIds.Count == 0)
            //     return;
            // if (!ReferenceEquals(GameMain.localPlanet, _planet) || !ReferenceEquals(GameMain.localPlanet?.factory, _factory))
            // {
            //     Log.Debug($"Clearing {_bpIds.Count} blue print previews that were created for a different planet");
            //     Clear();
            //     return;
            // } 

            if (GameMain.mainPlayer.orders.orderCount > 1)
            {
                Log.Debug("found existing orders, not adding new ones");
                return;
            }

            var points = CollectPoints();
            if (points.Count == 0)
            {
                return;
            }

            var groupedPoints = GroupPoints(points);

            if (groupedPoints.Count == 0)
            {
                Log.Warn($"grouped points was 0, that is not right when points was {points.Count}");
                return;
            }

            var curIntPos = ToIntVector(GameMain.mainPlayer.position);
            var possiblyStuck = curIntPos.Equals(_positionWhenLastOrderGiven) && (DateTime.Now - _lastOrderCreatedAt).TotalSeconds > 5;
            if (_lastOrder == null || _lastOrder.targetReached || (DateTime.Now - _lastOrderCreatedAt).TotalSeconds > 5)
            {
                var group = groupedPoints[0];
                if (group.Count == 0)
                {
                    Log.Warn("got back an empty group, that is not good");
                    if (groupedPoints.Count > 1)
                        group = groupedPoints[0];
                }

                if (group.Count == 0)
                {
                    return;
                }
                

                SortByDistance(group, GameMain.mainPlayer.position);
                var nextPos = possiblyStuck && group.Count > 1 ? group[1] : group[0];
                int latIndex = GetLatitudeIndex(nextPos);
                _lastOrder = OrderNode.MoveTo(nextPos);
                _lastOrderCreatedAt = DateTime.Now;
                _positionWhenLastOrderGiven = ToIntVector(GameMain.mainPlayer.position);
                GameMain.mainPlayer.Order(_lastOrder, true);
            }
            else
            {
                Log.Debug($"last order {_lastOrder?.targetReached} {_lastOrder?.target}");
            }
        }

        private static Vector3Int ToIntVector(Vector3 position)
        {
            return new Vector3Int((int)position.x, (int)position.y, (int)position.z);
        }

        private List<List<Vector3>> GroupPoints(List<Vector3> points)
        {
            var result = new List<List<Vector3>>();
            var lastNdx = -1;
            var curGroup = new List<Vector3>();
            foreach (var point in points)
            {
                int latIndex = GetLatitudeIndex(point);
                if (lastNdx < latIndex && lastNdx != -1)
                {
                    // new group
                    result.Add(curGroup);
                    curGroup = new List<Vector3>();
                }

                curGroup.Add(point);
                lastNdx = latIndex;
            }

            if (!result.Contains(curGroup))
            {
                result.Add(curGroup);
            }

            Log.Debug($"grouped {points.Count} into {result.Count} groups");

            return result;
        }

        private static int GetLatitudeIndex(Vector3 point)
        {
            Maths.GetLatitudeLongitude(point, out int latd, out _, out _, out _, out bool north, out _, out _,
                out _);
            var sign = north ? 1 : -1;
            return 180/4 - Mathf.FloorToInt((latd / 4) * sign + 90/4);
        }

        private static List<Vector3> CollectPoints()
        {
            var intPoints = new HashSet<Vector3Int>();
            var missingIds = new HashSet<int>();
            var points = new List<Vector3>();
            int ctr = 0;
            foreach (var prebuildData in GameMain.localPlanet.factory.prebuildPool)
            {
                if (prebuildData.id < 1)
                {
                    continue;
                }

                ctr++;
                if (missingIds.Contains(prebuildData.protoId))
                    continue;
                if (!InventoryManager.IsItemInInventoryOrInbound(prebuildData.protoId))
                {
                    var itemName = ItemUtil.GetItemName(prebuildData.protoId);
                    Log.LogPopupWithFrequency($"Item '{itemName}' not in inventory and not requested, skipping location");
                    missingIds.Add(prebuildData.protoId);
                    continue;
                }
                var intPos = ToIntVector(prebuildData.pos);
                if (intPoints.Contains(intPos))
                {
                    continue;
                }

                intPoints.Add(intPos);

                points.Add(prebuildData.pos);
            }

            if (points.Count == 0)
            {
                Log.LogPopupWithFrequency("Completed Build Preview building");
                Clear();
            }

            SortByLatIndex(points);
            return points;
        }

        private static void SortByLatIndex(List<Vector3> points)
        {
            points.Sort((p1, p2) =>
            {
                var p1Distance = GetLatitudeIndex(p1);
                var p2Distance = GetLatitudeIndex(p2);

                return p1Distance.CompareTo(p2Distance);
            });
        }

        private static void SortByDistance(List<Vector3> points, Vector3 refPoint)
        {
            points.Sort((p1, p2) =>
            {
                var p1Distance = Vector3.Distance(refPoint, p1);
                var p2Distance = Vector3.Distance(refPoint, p2);

                return p1Distance.CompareTo(p2Distance);
            });
        }
        //
        // [HarmonyPostfix]
        // [HarmonyPatch(typeof(BuildTool_BlueprintPaste), "CreatePrebuilds")]
        // public static void RecordPrebuildsFromBP(BuildTool_BlueprintPaste __instance)
        // {
        //     _bpIds.Clear();
        //     for (int index1 = 0; index1 < __instance.bpCursor; ++index1)
        //     {
        //         BuildPreview buildPreview = __instance.bpPool[index1];
        //         if (buildPreview.bpgpuiModelId > 0 && (buildPreview.condition == EBuildCondition.Ok || buildPreview.condition == EBuildCondition.NotEnoughItem) &&
        //             buildPreview.coverObjId == 0)
        //         {
        //             if (buildPreview.objId < 0)
        //             {
        //                 _bpIds.Add(-buildPreview.objId);
        //             }
        //         }
        //     }
        //
        //     _planet = __instance.planet;
        //     _factory = __instance.factory;
        //
        //     Log.Debug($"found {_bpIds.Count} ids that were actually blueprinted");
        // }

        public static void Clear()
        {
            _planet = null;
            _factory = null;
            _bpIds.Clear();
        }
    }
}