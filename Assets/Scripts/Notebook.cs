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
    //––– Define your pools of problems here –––
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
      "Solve for θ: 2cos²θ − 1 = 0 (0 ≤ θ < 2π)",
      "Evaluate: limₓ→0 (sin x)/x",
      "Find: arctan(1)",
      "Find: cos(45°)",
      "Simplify: tan(135°)",
      "Evaluate: limₓ→∞ (2x² + 3)/(x² - 1)",
      "Solve for θ: sin θ = √2/2 (0 ≤ θ < 2π)"
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
    [SerializeField] private GameObject notebookCanvas;
    [SerializeField] private TextMeshProUGUI problemText1;
    [SerializeField] private TextMeshProUGUI problemText2;
    [SerializeField] private TextMeshProUGUI problemText3;
    [SerializeField] private TMP_InputField answerInput1;
    [SerializeField] private TMP_InputField answerInput2;
    [SerializeField] private TMP_InputField answerInput3;
    [SerializeField] private Button submitButton;

    // Tracks the currently displayed problems
    private string currentProblem1;
    private string currentProblem2;
    private string currentProblem3;

    // Lookup of correct answers
    private readonly Dictionary<string, string> problemAnswers = new Dictionary<string, string>
    {
      // Algebra 2
      {"Solve for x: 2x + 5 = 13", "4"},
      {"Factor: x² − 9x + 20", "(x-4)(x-5)"},
      {"Simplify: (3x²y)(−2xy³)", "-6x^3y^4"},
      {"Solve for x: 3x - 7 = 2", "3"},
      {"Solve for x: x² - 4x - 5 = 0", "x=5 or x=-1"},
      {"Simplify: (5x³)(-2x²)", "-10x^5"},
      {"Factor: x² - 16", "(x-4)(x+4)"},
      {"Simplify: (4x²y)(3xy²)", "12x^3y^3"},
      {"Solve for x: |2x - 3| = 5", "x=4 or x=-1"},
      // Pre-Calc
      {"Evaluate limₓ→2 (x² − 4)/(x − 2)", "4"},
      {"Find: sin(150°)", "1/2"},
      {"Solve for θ: 2cos²θ − 1 = 0 (0 ≤ θ < 2π)", "π/4, 3π/4, 5π/4, 7π/4"},
      {"Evaluate: limₓ→0 (sin x)/x", "1"},
      {"Find: arctan(1)", "π/4"},
      {"Find: cos(45°)", "√2/2"},
      {"Simplify: tan(135°)", "-1"},
      {"Evaluate: limₓ→∞ (2x² + 3)/(x² - 1)", "2"},
      {"Solve for θ: sin θ = √2/2 (0 ≤ θ < 2π)", "π/4, 3π/4"},
      // Calculus
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

    private bool isPlayerInRange = false;

    private void Start()
    {
      if (notebookCanvas != null)
        notebookCanvas.SetActive(false);

      if (submitButton != null)
        submitButton.onClick.AddListener(SubmitAnswers);

      ClearProblemTexts();
      ClearInputFields();
    }

    private void Update()
    {
      if (isPlayerInRange && Input.GetKeyDown(KeyCode.E))
        ToggleCanvas();
    }

    private void OnTriggerEnter(Collider other)
    {
      if (other.CompareTag("Player"))
      {
        isPlayerInRange = true;
        Debug.Log("Press 'E' to interact with the notebook.");
      }
    }

    private void OnTriggerExit(Collider other)
    {
      if (other.CompareTag("Player"))
        isPlayerInRange = false;
    }

    private void ToggleCanvas()
    {
      if (notebookCanvas == null) return;

      bool isActive = notebookCanvas.activeSelf;
      notebookCanvas.SetActive(!isActive);

      if (!isActive)
      {
        Time.timeScale = 0f;
        currentProblem1 = GetRandomProblem(); problemText1.text = currentProblem1;
        currentProblem2 = GetRandomProblem(); problemText2.text = currentProblem2;
        currentProblem3 = GetRandomProblem(); problemText3.text = currentProblem3;
        ClearInputFields();

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
      }
      else
      {
        Time.timeScale = 1f;
        ClearProblemTexts();
        ClearInputFields();
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
      }
    }

    private void SubmitAnswers()
    {
      CheckAnswer(answerInput1, currentProblem1);
      CheckAnswer(answerInput2, currentProblem2);
      CheckAnswer(answerInput3, currentProblem3);

      bool allCorrect = IsAnswerCorrect(answerInput1.text.Trim(), currentProblem1)
               && IsAnswerCorrect(answerInput2.text.Trim(), currentProblem2)
               && IsAnswerCorrect(answerInput3.text.Trim(), currentProblem3);
      if (allCorrect)
        ToggleCanvas();
    }

    private void CheckAnswer(TMP_InputField inputField, string problem)
    {
      bool correct = IsAnswerCorrect(inputField.text.Trim(), problem);
      inputField.image.color = correct ? Color.green : Color.red;
      if (!correct)
        Debug.Log($"Incorrect. Correct answer for '{problem}' is: {problemAnswers[problem]}");
    }

    private bool IsAnswerCorrect(string userAnswer, string problem)
    {
      if (!problemAnswers.TryGetValue(problem, out string correctAnswer))
        return false;

      // Special handling for factoring: order-insensitive
      if (problem.StartsWith("Factor:"))
        return CompareFactors(userAnswer, correctAnswer);

      return string.Equals(userAnswer, correctAnswer, StringComparison.OrdinalIgnoreCase);
    }

    private bool CompareFactors(string userAns, string correctAns)
    {
      // Extract factors inside parentheses
      var userFactors = Regex.Matches(userAns.Replace(" ", ""), "\\([^)]*\\)")
                  .Cast<Match>()
                  .Select(m => m.Value)
                  .ToList();
      var correctFactors = Regex.Matches(correctAns.Replace(" ", ""), "\\([^)]*\\)")
                     .Cast<Match>()
                     .Select(m => m.Value)
                     .ToList();

      if (userFactors.Count != correctFactors.Count)
        return false;

      // Compare as multisets
      var userMultiset = userFactors.GroupBy(f => f)
                      .ToDictionary(g => g.Key, g => g.Count());
      var correctMultiset = correctFactors.GroupBy(f => f)
                        .ToDictionary(g => g.Key, g => g.Count());

      return userMultiset.Count == correctMultiset.Count
          && userMultiset.All(kvp => correctMultiset.TryGetValue(kvp.Key, out int cnt) && cnt == kvp.Value);
    }

    private void ClearProblemTexts()
    {
      problemText1.text = string.Empty;
      problemText2.text = string.Empty;
      problemText3.text = string.Empty;
    }

    private void ClearInputFields()
    {
      answerInput1.text = string.Empty;
      answerInput2.text = string.Empty;
      answerInput3.text = string.Empty;
      answerInput1.image.color = Color.white;
      answerInput2.image.color = Color.white;
      answerInput3.image.color = Color.white;
    }

    public string GetRandomProblem()
    {
      List<string> pool = MainMenu.difficulty switch
      {
        1 => algebra2Problems,
        2 => preCalcProblems,
        3 => calculusProblems,
        _ => algebra2Problems
      };
      return pool[UnityEngine.Random.Range(0, pool.Count)];
    }
  }
}
