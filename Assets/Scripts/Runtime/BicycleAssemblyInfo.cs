using System;
using UnityEngine;

namespace MechMaster.Runtime
{
    public static class BicycleAssemblyInfo
    {
        public static readonly string[] OrderedIds =
        {
            "controls_cables",
            "saddle_seatpost",
            "pedals",
            "brake_front",
            "brake_rear",
            "chain",
            "front_derailleur",
            "rear_derailleur",
            "wheel_front",
            "wheel_rear",
            "crank_bottom_bracket",
            "cockpit_headset",
            "fork",
            "frame"
        };

        public static int IndexOf(string assemblyId)
        {
            return Array.IndexOf(OrderedIds, assemblyId);
        }

        public static Vector3 TrayCellPosition(string assemblyId)
        {
            int index = Mathf.Max(0, IndexOf(assemblyId));
            int column = index % 7;
            int row = index / 7;
            return new Vector3(
                -1.14f + column * 0.38f,
                -0.43f,
                -0.46f - row * 0.3f);
        }

        public static Vector3 PartPosition(string assemblyId, int slotIndex)
        {
            Vector3 cell = TrayCellPosition(assemblyId);
            int column = slotIndex % 7;
            int row = (slotIndex / 7) % 7;
            int layer = slotIndex / 49;
            return cell + new Vector3(
                (column - 3) * 0.032f,
                0.055f + layer * 0.018f,
                (row - 3) * 0.027f);
        }

        public static string DisplayName(string assemblyId)
        {
            switch (assemblyId)
            {
                case "controls_cables":
                    return "操控与线管";
                case "cockpit_headset":
                    return "把组与碗组";
                case "brake_front":
                    return "前刹车";
                case "brake_rear":
                    return "后刹车";
                case "wheel_front":
                    return "前轮";
                case "wheel_rear":
                    return "后轮";
                case "chain":
                    return "链条";
                case "crank_bottom_bracket":
                    return "曲柄与中轴";
                case "front_derailleur":
                    return "前拨";
                case "rear_derailleur":
                    return "后拨";
                case "pedals":
                    return "脚踏";
                case "saddle_seatpost":
                    return "鞍座与座管";
                case "fork":
                    return "避震前叉";
                case "frame":
                    return "车架";
                default:
                    return string.IsNullOrEmpty(assemblyId) ? "其他" : assemblyId;
            }
        }
    }
}
