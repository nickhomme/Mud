//
// Created by Nicholas Homme on 1/16/20.
//
#ifdef __clang__
#pragma clang diagnostic push
#pragma clang diagnostic ignored "-Wswitch"
#endif
#ifndef CT400_SRC_JVM_JAVA_ARG_H_
#define CT400_SRC_JVM_JAVA_ARG_H_

#include <jni.h>
#include <signal.h>
#include "../src/memory-util.h"

typedef enum _Java_Object_Type {
  Java_Object_String = 1,
  Java_Object_Class,
  Java_Object_Throwable,
  Java_Object_Array,
  Java_Object_Custom
} Java_Object_Type;

typedef enum _Java_Type {
  Java_Int = 1,
  Java_Bool,
  Java_Byte,
  Java_Char,
  Java_Short,
  Java_Long,
  Java_Float,
  Java_Double,
  Java_Object,
  Java_Void,
} Java_Type;

#endif //CT400_SRC_JVM_JAVA_ARG_H_
#ifdef __clang__

#pragma clang diagnostic pop
#endif