#!/usr/bin/env bash

xelatex main-thesis &&
biber main-thesis &&
xelatex main-thesis &&
xelatex main-thesis &&
cp ./main-thesis.pdf ./build/lastest.pdf
