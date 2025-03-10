﻿import typescript from '@rollup/plugin-typescript';
import { minify } from "terser";
import path from 'path';
import fs from 'fs';
import pkg from "./package.json";
import {builtinModules} from "module";
import dts from "rollup-plugin-dts";

var globals = {
	'jquery': '$',
	'flatpickr': 'flatpickr',
	'tslib': 'tslib'
}

function normalizePath(fileName) {
    return fileName.split(path.win32.sep).join(path.posix.sep);
}

var outputs = [];

var rxNamespace = /^namespace\s?/;
var rxExport = /^export\s?/;
var rxDeclare = /^declare\s?/;
var rxDeclareGlobal = /^(declare\s+global.*\s*\{\r?\n)((^\s+.*\r?\n)*)?\}/gm;
var rxRemoveExports = /^export\s*\{[^\}]*\}\s*\;\s*\r?\n/gm;
var rxReferenceTypes = /^\/\/\/\s*\<reference\s*types\=\".*\r?\n/gm

var toGlobal = function(ns) {
	return {
		name: 'toGlobal',
		generateBundle(o, b) {
			for (var fileName of Object.keys(b)) {
				if (b[fileName].code && fileName.indexOf('.bundle.d.ts') >= 0) {
					var src = b[fileName].code;
					src = src.replace(/\r/g, '');
					src = src.replace(rxRemoveExports, '');

					var refTypes = [];
					src = src.replace(rxReferenceTypes, function(match, offset, str) {
						refTypes.push(match);
						return '';
					});

					var globals = [];
					src = src.replace(rxDeclareGlobal, function(x, y, m1) {
						var g = m1.replace(/(\r?\n)*$/, '').split('\n')
							.map(s => s.length > 0 && s.charAt(0) == '\t' ? s.substring(1) : (s.substring(0, 4) == '    ' ? s.substring(4) : s))
							.map(s => {
								if (rxNamespace.test(s))
									return ("declare " + s);
								else if (rxExport.test(s))
									return "declare " + s.substring(7);
								else
									return s;
							})
							.join('\n');
						globals.push(g);
						return '';
					});

					if (ns) {
						src = 'declare namespace ' + ns + ' {\n' + 
							src.replace(/(\r?\n)*$/, '').split('\n').map(s => {
								if (!s.length)
									return '';
								if (rxDeclare.test(s))
									return '    ' + s.substring(8);
								else
									return '    ' + s;
							}).join('\n') + '\n}'
					}

					src = refTypes.join('') + '\n' + src + '\n' + globals.join('\n');

					outputs.push(src);

					var toEmit = {
						type: 'asset',
						fileName: fileName.replace('.bundle.d.ts', '.global.d.ts'),
						source: src
					};
					this.emitFile(toEmit);
				}
			}
		}
	}
}

var extendGlobals = function() {
	return {
		name: 'extendGlobals',
		generateBundle(o, b) {
			for (var fileName of Object.keys(b)) {
				var code = b[fileName].code || b[fileName].source;

				if (code && fileName.indexOf('.js') >= 0) {
					var src = code;
					src = fs.readFileSync('./node_modules/tslib/tslib.js',
						'utf8').replace(/^\uFEFF/, '') + '\n' + src;
					src = src.replace(/^(\s*)exports\.([A-Za-z_]+)\s*=\s*(.+?);/gm, function(match, grp1, grp2, grp3) {
						if (grp2.charAt(0) == '_' && grp2.charAt(1) == '_')
							return grp1 + "exports." + grp2 + " = exports." + grp2 + " || " + grp3 + ";";
						return grp1 + "exports." + grp2 + " = exports." + grp2 + " || {}; extend(exports." + grp2 + ", " + grp3 + ");";
					});
					src = src.replace(/,\s*tslib/g, '');
					src = src.replace(/tslib\.__/g, '__');
					b[fileName].code = src;
				}
				else if (code && /(Globals\/.*|Globals)\.d\.ts$/i.test(fileName) && code.indexOf('declare global') < 0) {
					outputs.push(code);
				}
			}
		}
	}
}

var writeMinJS = function() {
	return {
		name: 'writeMinJS',
		generateBundle: async function(o, b) {
			var self = this;
			Object.keys(b).forEach(async function(fileName) {
				if (b[fileName].code && /\.js$/i.test(fileName) >= 0) {
					var src = b[fileName].code;
					var minified = await minify(src, {
						mangle: true,
						format: {
							beautify: false,
							max_line_len: 1000
						}
					});
					var toEmit = {
						type: 'asset',
						fileName: fileName.replace(/\.js$/, '.min.js'),
						source: minified.code
					};
					self.emitFile(toEmit);
				}
			});
		}
	}
}

export default [
	{
		input: "CoreLib/CoreLib.ts",
		output: [
			{
				file: 'dist/Serenity.CoreLib.js',
				format: "iife",
				sourcemap: true,
				name: "window",
				extend: true,
				freeze: false,
				globals
			}
		],
		plugins: [
			typescript({
				tsconfig: 'CoreLib/tsconfig.json',
				outDir: 'CoreLib/built'
			}),
			extendGlobals(),
			writeMinJS()
		],
		external: [
			...builtinModules,
			...(pkg.dependencies == null ? [] : Object.keys(pkg.dependencies)),
			...(pkg.devDependencies == null ? [] : Object.keys(pkg.devDependencies)),
			...(pkg.peerDependencies == null ? [] : Object.keys(pkg.peerDependencies))
		]
	},
	{
		input: "./dist/built/Q/indexAll.d.ts",
		output: [{ file: "./dist/built/Q/indexAll.bundle.d.ts", format: "es" }],
		plugins: [dts(), toGlobal('Q')],
	},
	{
		input: "./dist/built/Serenity/index.d.ts",
		output: [{ file: "./dist/built/Serenity/index.bundle.d.ts", format: "es" }],
		plugins: [dts(), toGlobal('Serenity')],
	},
	{
		input: "./dist/built/Slick/index.d.ts",
		output: [{ file: "./dist/built/Slick/index.bundle.d.ts", format: "es" }],
		plugins: [dts(), toGlobal('Slick'), {
			name: 'writeFinal',
			generateBundle: function () {
				outputs.splice(0, 0, fs.readFileSync('./node_modules/tslib/tslib.d.ts',
					'utf8').replace(/^\uFEFF/, '').replace(/^[ \t]*export declare/gm, 'declare'));

				var src = outputs.join('\n').replace(/\r/g, '');
				var refTypes = [];
				src = src.replace(rxReferenceTypes, function(match, offset, str) {
					refTypes.push(match);
					return '';
				});
				refTypes = refTypes.filter((x, i) => refTypes.indexOf(x) == i);
				src = refTypes.join('') + '\n' + src
				fs.writeFileSync('./dist/Serenity.CoreLib.d.ts', src);
			}
		}]
	}
];