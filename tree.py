#!/usr/bin/env python3
# -*- coding: utf-8 -*-

"""
目录树生成器
递归遍历指定目录，打印出类似 'tree' 命令的层级结构。
默认将树形结构保存到 tree.txt 文件。
"""

import os
import sys
import argparse

def print_tree(startpath='.', ignore_hidden=True, output_file=None, ascii=False):
    """
    生成并输出目录树

    :param startpath:   起始目录路径
    :param ignore_hidden: 是否忽略以点开头的隐藏文件和目录
    :param output_file:  输出文件路径（None 则打印到控制台）
    :param ascii:        是否使用 ASCII 字符代替 Unicode 线条
    """
    # 选择线条字符
    if ascii:
        branch = '|-- '
        corner = '`-- '
        vertical = '|   '
        space = '    '
    else:
        branch = '├── '
        corner = '└── '
        vertical = '│   '
        space = '    '

    # 打开输出文件（如果指定）
    fout = None
    if output_file:
        try:
            fout = open(output_file, 'w', encoding='utf-8')
        except IOError as e:
            print(f"无法写入文件 {output_file}: {e}", file=sys.stderr)
            sys.exit(1)

    def _write(line):
        """写入一行到输出目标"""
        if fout:
            fout.write(line + '\n')
        else:
            print(line)

    def _walk(dirpath, prefix=''):
        """递归遍历目录"""
        try:
            entries = sorted(os.listdir(dirpath))
        except PermissionError:
            _write(prefix + branch + '[权限不足]')
            return

        # 分离目录和文件，以便目录在前（可选），这里保持字母顺序
        for i, entry in enumerate(entries):
            if ignore_hidden and entry.startswith('.'):
                continue

            fullpath = os.path.join(dirpath, entry)
            is_last = (i == len(entries) - 1)

            # 当前条目前缀
            connector = corner if is_last else branch
            _write(prefix + connector + entry + ('/' if os.path.isdir(fullpath) else ''))

            if os.path.isdir(fullpath):
                extension = space if is_last else vertical
                _walk(fullpath, prefix + extension)

    # 打印根目录名
    root = os.path.basename(os.path.abspath(startpath)) or startpath
    _write(root + '/')
    _walk(startpath, '')

    if fout:
        fout.close()
        print(f"目录树已保存至: {output_file}", file=sys.stderr)


if __name__ == '__main__':
    parser = argparse.ArgumentParser(
        description='生成并显示当前目录（或指定目录）的树形结构。默认输出到 tree.txt 文件。'
    )
    parser.add_argument(
        'path', nargs='?', default='.',
        help='要扫描的根目录路径（默认为当前目录）'
    )
    parser.add_argument(
        '-o', '--output', metavar='FILE',
        help='将树形结构输出到指定文件（默认为 tree.txt）'
    )
    parser.add_argument(
        '--show-hidden', action='store_false', dest='ignore_hidden',
        help='显示隐藏文件和目录（以点开头）'
    )
    parser.add_argument(
        '--ascii', action='store_true',
        help='使用 ASCII 字符代替 Unicode 线条（兼容老旧终端）'
    )
    args = parser.parse_args()

    # 检查路径是否存在
    if not os.path.exists(args.path):
        print(f"错误：路径 '{args.path}' 不存在。", file=sys.stderr)
        sys.exit(1)

    # 若未指定输出文件，默认输出到 tree.txt
    if args.output is None:
        args.output = 'tree.txt'

    print_tree(
        startpath=args.path,
        ignore_hidden=args.ignore_hidden,
        output_file=args.output,
        ascii=args.ascii
    )