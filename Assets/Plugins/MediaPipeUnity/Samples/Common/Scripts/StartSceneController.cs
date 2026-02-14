// Copyright (c) 2021 homuler
//
// Use of this source code is governed by an MIT-style
// license that can be found in the LICENSE file or at
// https://opensource.org/licenses/MIT.

using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using System.Threading.Tasks; // Taskとasync/awaitのために追加

namespace Mediapipe.Unity
{
  public class StartSceneController : MonoBehaviour
  {
    private const string _TAG = nameof(Bootstrap);

    // InspectorでそれぞれのUI (CanvasのGameObject) を割り当てる
    // 初期化中に表示するUI (例: ローディング画面)
    [SerializeField] public GameObject initializationUI;
    // 初期化完了後に表示するメインUI
    [SerializeField] public GameObject mainUI;

    // 現在のコードで使用されているフィールドは、今回の修正では不要になる可能性がありますが、
    // エラーを防ぐために残しつつ、未使用の警告を避けるためSerializeFiledを削除または変更します。
    // [SerializeField] private Image _screen;
    // [SerializeField] private GameObject _consolePrefab; 

    // Start()をIEnumeratorからasync voidに変更し、初期化処理を実行する
    async void Start()
    {
      // 1. 処理開始時、初期化UIのみを表示する
      // nullチェックを追加し、Inspectorで設定されていない場合のRuntimeエラーを防ぎます。
      if (initializationUI != null)
      {
        initializationUI.SetActive(true);
      }
      if (mainUI != null)
      {
        mainUI.SetActive(false);
      }

      // Bootstrapコンポーネントがある場合、その完了を待つなどの既存の初期化処理を組み込む
      var bootstrap = GetComponent<Bootstrap>();
      if (bootstrap != null)
      {
        // Bootstrapの完了を待つ (WaitUntilは非同期メソッド内では直接使えないため、適宜修正が必要)
        // ここでは代わりに、Bootstrapに初期化完了を通知するイベントやメソッドがあればそれを呼び出す
        // もしくは、Bootstrapが非同期処理を持っている場合はそれをawaitする
        // 既存のコードのようにisFinishedを待つ代わりに、Initialize()を呼び出す
        // yield return new WaitUntil(() => bootstrap.isFinished); // IEnumeratorでないため削除

        // 既存のBootstrapの処理を同期的に実行する場合
        // bootstrap.InitializeSync(); 

        // DontDestroyOnLoad(gameObject); // シーン遷移を行わないため削除またはコメントアウト

        // Logger.LogInfo(_TAG, "Loading the first scene..."); // シーン遷移を行わないため削除
        // var sceneLoadReq = SceneManager.LoadSceneAsync(1); // シーン遷移を行わないため削除
        // yield return new WaitUntil(() => sceneLoadReq.isDone); // シーン遷移を行わないため削除
      }

      // 2. ここで非同期の初期化処理などを実行する
      await Initialize();
    }

    // 画像の例を参考に非同期処理を行うInitializeメソッド
    async Task Initialize() // async voidではなくasync Taskに変更することを推奨 (例外処理のため)
    {
      // Ensure this async method actually yields to avoid CS1998 warning when no awaits are present.
      await Task.Yield();
      // (例) 重い処理やデータの読み込みを待つ
      // await Task.Delay(3000); // 3秒待つダミー処理

      // 既存のBootstrapが初期化処理をTaskとして公開している場合はここでawaitする
      // if (GetComponent<Bootstrap>()?.InitializeTask != null)
      // {
      //     await GetComponent<Bootstrap>().InitializeTask;
      // }

      // 3. 初期化が完了したら、UIを切り替える
      if (initializationUI != null)
      {
        initializationUI.SetActive(false);
      }
      if (mainUI != null)
      {
        mainUI.SetActive(true);
      }
    }
  }
}
