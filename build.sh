#!/usr/bin/env bash

xelatex main-thesis &&
biber main-thesis &&
xelatex main-thesis &&
xelatex main-thesis
