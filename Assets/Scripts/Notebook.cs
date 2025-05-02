using System.Collections.Generic;
using UnityEngine;

namespace WrathOfHerndon
{
    public class Notebook : MonoBehaviour
    {
        //––– Define your pools of problems here –––
        private readonly List<string> algebra2Problems = new List<string>()
        {
            "Solve for x:  2x + 5 = 13",
            "Factor:  x² − 9x + 20",
            "Simplify:  (3x²y)(−2xy³)"
        };

        private readonly List<string> preCalcProblems = new List<string>()
        {
            "Evaluate:  limₓ→2 (x² − 4)/(x − 2)",
            "Find the exact value:  sin(150°)",
            "Solve for θ:  2cos²θ − 1 = 0  (0 ≤ θ < 2π)"
        };

        private readonly List<string> calculusProblems = new List<string>()
        {
            "Differentiate:  f(x) = x³ · eˣ",
            "Integrate:  ∫ (2x)/(x² + 1) dx",
            "Find the critical points of  f(x) = x⁴ − 4x² + 1"
        };

        /// <summary>
        /// Returns a random problem string based on the current difficulty.
        /// 1 → Algebra 2, 2 → Pre-Calc, 3 → Calc
        /// </summary>
        public string GetRandomProblem()
        {
            List<string> pool;
            switch (MainMenu.difficulty)
            {
                case 1:
                    pool = algebra2Problems;
                    break;
                case 2:
                    pool = preCalcProblems;
                    break;
                case 3:
                    pool = calculusProblems;
                    break;
                default:
                    // safety fallback
                    pool = algebra2Problems;
                    Debug.LogWarning($"Unknown difficulty {MainMenu.difficulty}, defaulting to Algebra 2");
                    break;
            }

            int idx = Random.Range(0, pool.Count);
            return pool[idx];
        }

        // Example: on Start we log one problem
        private void Start()
        {
            string problem = GetRandomProblem();
            Debug.Log($"[Notebook] Difficulty {MainMenu.difficulty} problem: {problem}");
        }
    }
}
