# PuniPuniReinaSakiria

## これは何?

ReinaSakiria が [ぷにぷにアバター](https://x.com/rio3dWorks/status/1885366571616657439) というのを見て自分で描いて作ってた時の、アバターとしてのセットアップ部分だけを公開した物です。

## ぷにぷにアバターとの違い

ない物

- 綺麗な伸び縮み
- 連結機能
- ボイス
- コライダージャンプ
- 吹き出し

そして、PPRS(PuniPuniReinaSakiria) は Reina_Sakiria がイラストを描くときに各枚数を減らしたかったので、私が普段飛んでることが多いを利用して、歩行ではなく飛行の立ち絵を使うことにしたため、この PPRS は移動時に飛行することを想定したものになっています。

## これを構築する技術

- VRC PhysBone
- VRC RotationConstraint
- [AnimatorAsCodeV1](https://github.com/hai-vr/av3-animator-as-code)

基本的に、すべてのアニメーションが AnimatorAsCode で構築されています。

他の要素は移動時などのうごきを適当に作るために使われています。

## 使い方

Prefab の PPRS の中にある PuniPuniReinaSakiriaRenderer というメッシュレンダラーにマテリアルを適当に割り当ててください。

テクスチャは、正方形四枚 左から順に

- 通常(ニュートラル)
- 瞬き(目を閉じた差分)
- リップシンク(口を開いた差分)
- 飛行(移動時の差分)

この通りに、並んだものであれば使用できると思います。

C# が扱える方は、この PuniPuniReinaSakiria は MIT License なので自由に Fork して魔改造しちゃってくださいね！
