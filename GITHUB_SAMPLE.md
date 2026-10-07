# githubの利用方法

# 1. テンプレートをコピーして名前を変更から新しいディレクトリを作成して移動

# 2. .gitリポジトリとして初期化
-  git init
-  git add .
# 3. 最初のコミット(空でも可)
- git commit -m "initial commit"
# 4. GitHubにプライベートリポジトリを作成してプッシュ
- gh repo create 000_*** --private --source=. --remote=origin --push

# 5. 初回のコミット・プッシュ
- git remote set-url origin https://github.com/yamamotowt08/000_***.git

- git remote -v
- git status
- git push origin main
