using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace WrathOfHerndon
{
    public class Notebook : MonoBehaviour
    {
        //––– Problem pools for each difficulty –––
        private readonly List<string> algebra2Problems = new List<string>()
        {
            "Solve for x: 2x + 5 = 13",
            "Factor: x² − 9x + 20",
            "Simplify: (3x²y)(−2xy³)",
            "Solve for x: 3x - 7 = 2",
            "Solve for x: x² - 4x - 5 = 0",
            "Simplify: (5x³)(-2x²)",
            "Factor: x² - 16",
            "Simplify: (4x²y)(3xy²)",
            "Solve for x: |2x - 3| = 5"
        };
        private readonly List<string> preCalcProblems = new List<string>()
        {
            "Evaluate: limₓ→2 (x² − 4)/(x − 2)",
            "Find: sin(150°)",
            "Solve for θ: 2cos²θ − 1 = 0 (0 ≤ θ < 2pi)",
            "Evaluate: limₓ→0 (sin x)/x",
            "Find: arctan(1)",
            "Find: cos(45°)",
            "Simplify: tan(135°)",
            "Evaluate: limₓ→∞ (2x² + 3)/(x² - 1)",
            "Solve for θ: sin θ = √2/2 (0 ≤ θ < 2pi)"
        };
        private readonly List<string> calculusProblems = new List<string>()
        {
            "Solve: d/dx ( x³ · eˣ )",
            "Integrate: ∫ (2x)/(x² + 1) dx",
            "Find critical points: f(x) = x⁴ − 4x² + 1",
            "Solve: d/dx ( ln(x² + 1) )",
            "Integrate: ∫ e^(2x) dx",
            "Solve: d/dx ( x² · sin(x) )",
            "Integrate: ∫ x cos(x) dx",
            "Derive: f(x) = 1/x",
            "Compute: ∫ ( 3x² dx ) from 0 to 1"
        };

        [Header("UI References")]
        [SerializeField] private GameObject notebookCanvas;       // Canvas that holds the UI
        [SerializeField] private TextMeshProUGUI problemText1;    // Displays problem 1
        [SerializeField] private TextMeshProUGUI problemText2;    // Displays problem 2
        [SerializeField] private TextMeshProUGUI problemText3;    // Displays problem 3
        [SerializeField] private TMP_InputField answerInput1;     // Input for answer 1
        [SerializeField] private TMP_InputField answerInput2;     // Input for answer 2
        [SerializeField] private TMP_InputField answerInput3;     // Input for answer 3
        [SerializeField] private Button submitButton;             // Submit button
        [SerializeField] private TextMeshProUGUI interactionPrompt;

        //––– Remaining pools to avoid repeats until scene reload –––
        private List<string> remainingAlgebra2;
        private List<string> remainingPreCalc;
        private List<string> remainingCalculus;

        // Tracks which problems are currently shown
        private string currentProblem1;
        private string currentProblem2;
        private string currentProblem3;

        // Map each problem to its correct solution
        private readonly Dictionary<string, string> problemAnswers = new Dictionary<string, string>
        {
            {"Solve for x: 2x + 5 = 13", "4"},
            {"Factor: x² − 9x + 20", "(x-4)(x-5)"},
            {"Simplify: (3x²y)(−2xy³)", "-6x^3y^4"},
            {"Solve for x: 3x - 7 = 2", "3"},
            {"Solve for x: x² - 4x - 5 = 0", "-5,1"},
            {"Simplify: (5x³)(-2x²)", "-10x^5"},
            {"Factor: x² - 16", "(x-4)(x+4)"},
            {"Simplify: (4x²y)(3xy²)", "12x^3y^3"},
            {"Solve for x: |2x - 3| = 5", "-1,4"},
            {"Evaluate: limₓ→2 (x² − 4)/(x − 2)", "4"},
            {"Find: sin(150°)", "1/2"},
            {"Solve for θ: 2cos²θ − 1 = 0 (0 ≤ θ < 2pi)", "pi/4, 3pi/4, 5pi/4, 7pi/4"},
            {"Evaluate: limₓ→0 (sin x)/x", "1"},
            {"Find: arctan(1)", "pi/4"},
            {"Find: cos(45°)", "√2/2"},
            {"Simplify: tan(135°)", "-1"},
            {"Evaluate: limₓ→∞ (2x² + 3)/(x² - 1)", "2"},
            {"Solve for θ: sin θ = √2/2 (0 ≤ θ < 2pi)", "pi/4, 3pi/4"},
            {"Solve: d/dx ( x³ · eˣ )", "e^x(3x^2 + x^3)"},
            {"Integrate: ∫ (2x)/(x² + 1) dx", "ln(x^2+1) + C"},
            {"Find critical points: f(x) = x⁴ − 4x² + 1", "x=0, ±√2"},
            {"Solve: d/dx ( ln(x² + 1) )", "2x/(x^2+1)"},
            {"Integrate: ∫ e^(2x) dx", "(1/2)e^(2x) + C"},
            {"Solve: d/dx ( x² · sin(x) )", "2x sin x + x^2 cos x"},
            {"Integrate: ∫ x cos(x) dx", "x sin x + cos x + C"},
            {"Derive: f(x) = 1/x", "-1/x^2"},
            {"Compute: ∫ ( 3x² dx ) from 0 to 1", "1"}
        };

        // Tracks if player is within interaction range
        private bool isPlayerInRange = false;

        // STATIC: which notebook (if any) currently has the UI open
        private static Notebook activeNotebook = null;

        // Ensures each notebook only draws its problems once
        private bool problemsInitialized = false;

        private void Start()
        {
            // Initialize remaining pools at scene load
            remainingAlgebra2 = new List<string>(algebra2Problems);
            remainingPreCalc = new List<string>(preCalcProblems);
            remainingCalculus = new List<string>(calculusProblems);

            // Ensure the notebook UI is hidden
            if (notebookCanvas != null)
                notebookCanvas.SetActive(false);

            // Attach the SubmitAnswers method to the button
            if (submitButton != null)
                submitButton.onClick.AddListener(SubmitAnswers);

            // Clear any placeholder text/inputs
            ClearProblemTexts();
            ClearInputFields();

            // Hide the prompt initially
            if (interactionPrompt != null)
            {
                interactionPrompt.text = "";
                interactionPrompt.gameObject.SetActive(false);
            }
        }

        private void Update()
        {
            // Only respond to "E" if player is in range
            if (isPlayerInRange && Input.GetKeyDown(KeyCode.E))
            {
                bool isOpen = notebookCanvas.activeSelf;

                // Open this notebook if none is active
                if (!isOpen && activeNotebook == null)
                {
                    activeNotebook = this;
                    ToggleCanvas();
                }
                // Close only if *this* notebook is active
                else if (isOpen && activeNotebook == this)
                {
                    ToggleCanvas();
                    activeNotebook = null;
                }
            }
        }

        private void OnTriggerEnter(Collider other)
        {
            if (other.CompareTag("Player"))
            {
                isPlayerInRange = true;

                // Show prompt only if no notebook is open
                if (activeNotebook == null && interactionPrompt != null)
                {
                    interactionPrompt.text = "Press E to open notebook";
                    interactionPrompt.gameObject.SetActive(true);
                }
            }
        }

        private void OnTriggerExit(Collider other)
        {
            if (other.CompareTag("Player"))
            {
                isPlayerInRange = false;

                // Hide prompt when you walk away
                if (interactionPrompt != null)
                {
                    interactionPrompt.text = "";
                    interactionPrompt.gameObject.SetActive(false);
                }
            }
        }

        private void ToggleCanvas()
        {
            if (notebookCanvas == null) return;

            bool open = !notebookCanvas.activeSelf;
            notebookCanvas.SetActive(open);

            if (open)
            {
                // Pause game & show mouse
                Time.timeScale = 0f;
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;

                // Hide the interaction prompt
                if (interactionPrompt != null)
                    interactionPrompt.gameObject.SetActive(false);

                // Only draw once, per-notebook
                if (!problemsInitialized)
                {
                    currentProblem1 = DrawUniqueProblem();
                    currentProblem2 = DrawUniqueProblem();
                    currentProblem3 = DrawUniqueProblem();
                    problemsInitialized = true;
                }

                // Display them
                problemText1.text = currentProblem1;
                problemText2.text = currentProblem2;
                problemText3.text = currentProblem3;
                ClearInputFields();
            }
            else
            {
                // Resume game & lock mouse
                Time.timeScale = 1f;
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;

                ClearProblemTexts();
                ClearInputFields();

                // If still in range, show the prompt again
                if (isPlayerInRange && interactionPrompt != null)
                {
                    interactionPrompt.text = "Press E to open notebook";
                    interactionPrompt.gameObject.SetActive(true);
                }
            }
        }

        private string DrawUniqueProblem()
        {
            List<string> pool = MainMenu.difficulty switch
            {
                1 => remainingAlgebra2,
                2 => remainingPreCalc,
                3 => remainingCalculus,
                _ => remainingAlgebra2
            };

            if (pool.Count == 0)
            {
                Debug.LogWarning("No more problems in this category. Reload to reset.");
                return string.Empty;
            }

            int idx = UnityEngine.Random.Range(0, pool.Count);
            string problem = pool[idx];
            pool.RemoveAt(idx);
            return problem;
        }

        private void SubmitAnswers()
        {
            // Only the active notebook should respond
            if (activeNotebook != this) return;

            CheckAnswer(answerInput1, currentProblem1);
            CheckAnswer(answerInput2, currentProblem2);
            CheckAnswer(answerInput3, currentProblem3);

            bool allCorrect =
                IsAnswerCorrect(answerInput1.text.Trim(), currentProblem1) &&
                IsAnswerCorrect(answerInput2.text.Trim(), currentProblem2) &&
                IsAnswerCorrect(answerInput3.text.Trim(), currentProblem3);

            if (!allCorrect)
                return;

            // close, collect, and destroy
            ToggleCanvas();
            GameManager.Instance.AddNotebook();
            Destroy(gameObject, 1.0f);
        }

        private void CheckAnswer(TMP_InputField inputField, string problem)
        {
            bool correct = IsAnswerCorrect(inputField.text.Trim(), problem);
            inputField.image.color = correct ? Color.green : Color.red;
            if (!correct)
                Debug.Log($"Incorrect. Correct answer for '{problem}' is {problemAnswers[problem]}");
        }

        private bool IsAnswerCorrect(string userAnswer, string problem)
        {
            if (!problemAnswers.TryGetValue(problem, out string correctAnswer))
                return false;

            if (problem.StartsWith("Factor:"))
                return CompareFactors(userAnswer, correctAnswer);

            return string.Equals(userAnswer, correctAnswer, StringComparison.OrdinalIgnoreCase);
        }

        private bool CompareFactors(string userAns, string correctAns)
        {
            var userFactors = Regex.Matches(userAns.Replace(" ", ""), "\\([^)]*\\)")
                                   .Cast<Match>().Select(m => m.Value);
            var correctFactors = Regex.Matches(correctAns.Replace(" ", ""), "\\([^)]*\\)")
                                       .Cast<Match>().Select(m => m.Value);

            var userMulti = userFactors.GroupBy(f => f).ToDictionary(g => g.Key, g => g.Count());
            var correctMulti = correctFactors.GroupBy(f => f).ToDictionary(g => g.Key, g => g.Count());

            return userMulti.Count == correctMulti.Count
                && userMulti.All(kvp => correctMulti.TryGetValue(kvp.Key, out int cnt) && cnt == kvp.Value);
        }

        private void ClearProblemTexts()
        {
            problemText1.text = "";
            problemText2.text = "";
            problemText3.text = "";
        }

        private void ClearInputFields()
        {
            answerInput1.text = "";
            answerInput2.text = "";
            answerInput3.text = "";
            answerInput1.image.color = Color.white;
            answerInput2.image.color = Color.white;
            answerInput3.image.color = Color.white;
        }

        private void OnDestroy()
        {
            // Ensure no dangling prompt
            if (interactionPrompt != null)
            {
                interactionPrompt.text = "";
                interactionPrompt.gameObject.SetActive(false);
            }
        }
    }
}
