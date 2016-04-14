PostfixCodeCompletion plugin for FlashDevelop
========================
[![Build status](https://ci.appveyor.com/api/projects/status/acnsq3sk2xboe3as?svg=true)](https://ci.appveyor.com/project/slavara/fd-postfix-code-completion-plugin)

The basic idea is to prevent caret jumps backwards while typing code.
Kind of surround templates on steroids baked with code completion.

## Minimum Requirements
* FlashDevelop 5.0.1 or never.
* Haxe 3.2.0 or never for haxe projects.

### Installation

Download the latest release. Open the .fdz file with FlashDevelop.

https://github.com/SlavaRa/fdplugin-postfix-code-completion/releases

## Features
Available templates for AS3:
* `.if` – checks boolean expression to be true `if (expr)`
* `.else` – checks boolean expression to be false `if (!expr)`
* `.null` – checks nullable expression to be null `if (expr == null)`
* `.notnull` – checks expression to be non-null `if (expr != null)`
* `.not` – negates value of inner boolean expression `!expr`
* `.foreach` – iterates over collection `foreach (var x in expr)`
* `.foin` - for Object surrounds with loop `for (var key:String in expr)`
* `.foin` - for Dictionary surrounds with loop `for (var key:Object in expr)`
* `.for` – for Array|Vector surrounds with loop `for (var i = 0; i < expr.length; i++)`
* `.for` – for Numeric surrounds with loop `for (var i = 0; i < expr; i++)`
* `.forr` – for Array|Vector reverse loop `for (var i = expr.length - 1; i >= 0; i--)`
* `.forr` – for Numeric reverse loop `for (var i = expr; i >= 0; i--)`
* `.var` – initialize new variable with expression `var x = expr;`
* `.const` – initialize new variable with expression `const x = expr;`
* `.new` – produces instantiation expression for type `new T()`
* `.par` – surrounds outer expression with parentheses `(expr)`
* `.return` – returns value from method/property `return expr;`
* `.while` – uses expression as loop condition `while (expr)`
* `.dowhile` – uses expression as loop condition  `do{...} while(expr);`
* `.sel` – selects expression in editor

Available templates for Haxe:
* `.code` - for String expression to be `expr.code`
* `.if` – checks boolean expression to be true  `if (expr)`
* `.else` – checks boolean expression to be false  `if (!expr)`
* `.null` – checks nullable expression to be null `if (expr == null)`
* `.notnull` – checks expression to be non-null `if (expr != null)`
* `.not` – negates value of inner boolean expression `!expr`
* `.foreach` – iterates over collection `for(it in expr`)
* `.for` – for Array|Vector|Iterator|Iterable surrounds with loop `for (i in 0...expr.Length)`
* `.for` – for Numeric surrounds with loop `for (i in 0...expr)`
* `.var` – initialize new variable with expression `var x = expr;`
* `.new` – produces instantiation expression for type `new T()`
* `.par` – surrounds outer expression with parentheses `(expr)`
* `.return` – returns value from method/property `return expr;`
* `.while` – uses expression as loop condition `while (expr)`
* `.dowhile` – uses expression as loop condition `do{...} while(expr);`
* `.sel` – selects expression in editor
