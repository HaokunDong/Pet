using UnityEngine;

namespace PetGame
{
    /// <summary>
    /// Utility class for calculating and formatting property values in the TrainView panel.
    /// Provides formulas for both standard attributes and the special CD attribute.
    /// </summary>
    public static class TrainPropertyCalculator
    {
        /// <summary>
        /// Result struct for standard property calculation.
        /// </summary>
        public struct PropertyResult
        {
            public float total;     // X = A + B + C
            public float baseValue; // A = base stat from CharacterData
            public float growth;    // B = A * (level - 1) * 0.01
            public float talent;    // C = talentLevel * 1
        }

        /// <summary>
        /// Result struct for CD property calculation (percentage-based).
        /// </summary>
        public struct CDPropertyResult
        {
            public float totalPercent;  // X% = B% + C%
            public float growthPercent; // B% = (level - 1) * 1%
            public float talentPercent; // C% = skillCDLevel * 1%
        }

        /// <summary>
        /// Calculate a standard property value (Attack, Defense, Health, Agility, AttackSpeed).
        /// Formula: X = A + B + C
        ///   A = baseValue (from CharacterData)
        ///   B = baseValue * (level - 1) * 0.01 (growth per level)
        ///   C = talentLevel * 1 (talent point bonus)
        /// </summary>
        public static PropertyResult CalculateProperty(float baseValue, int level, int talentLevel)
        {
            PropertyResult result;
            result.baseValue = baseValue;
            result.growth = baseValue * (level - 1) * 0.01f;
            result.talent = talentLevel * 1f;
            result.total = result.baseValue + result.growth + result.talent;
            return result;
        }

        /// <summary>
        /// Calculate the CD (cooldown reduction) property value.
        /// Formula: X% = B% + C%
        ///   B% = (level - 1) * 1% (growth per level)
        ///   C% = skillCDLevel * 1% (talent point bonus)
        /// No base value (A) for CD.
        /// </summary>
        public static CDPropertyResult CalculateCDProperty(int level, int skillCDLevel)
        {
            CDPropertyResult result;
            result.growthPercent = (level - 1) * 1f;
            result.talentPercent = skillCDLevel * 1f;
            result.totalPercent = result.growthPercent + result.talentPercent;
            return result;
        }

        /// <summary>
        /// Format a standard property value into display text.
        /// Format: "属性名：X（A+B+C）"
        /// Values are rounded to one decimal place; if the decimal is .0, display as integer.
        /// </summary>
        public static string FormatPropertyText(string propertyName, PropertyResult result)
        {
            string x = FormatNumber(result.total);
            string a = FormatNumber(result.baseValue);
            string b = FormatNumber(result.growth);
            string c = FormatNumber(result.talent);
            return $"{propertyName}： {x}（{a} + {b} + {c}）";
        }

        /// <summary>
        /// Format the CD property value into display text.
        /// Format: "冷却缩减：X%（B%+C%）"
        /// Values are rounded to one decimal place.
        /// </summary>
        public static string FormatCDPropertyText(CDPropertyResult result)
        {
            string x = FormatNumber(result.totalPercent);
            string b = FormatNumber(result.growthPercent);
            string c = FormatNumber(result.talentPercent);
            return $"冷却缩减： {x}%（{b}% + {c}%）";
        }

        /// <summary>
        /// Format a number to one decimal place. If the decimal part is 0, show as integer.
        /// </summary>
        private static string FormatNumber(float value)
        {
            // Round to one decimal place
            float rounded = Mathf.Round(value * 10f) / 10f;
            if (Mathf.Approximately(rounded % 1f, 0f))
            {
                return ((int)rounded).ToString();
            }
            return rounded.ToString("F1");
        }
    }
}
