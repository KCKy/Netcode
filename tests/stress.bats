#!/usr/bin/env bats

load setup.bash
load functions.bash

@test "stress test base" {
    prepare_log

    run_base server stress --tickrate 20 --delta 0.05 --sample-window 20 --checksum --server --duration 60 --target $SERVER_POINT
    server=$!
    sleep 1

    run_base client stress --tickrate 20 --delta 0.05 --sample-window 20 --checksum --target $TARGET 
    client=$!

    wait -n $server
    wait -n $client
}

@test "stress test many clients" {
    prepare_log

    run_base server stress --tickrate 20 --delta 0.05 --sample-window 20 --server --duration 120 --target $SERVER_POINT
    server=$!
    sleep 1

    run_base client1 stress --tickrate 20 --delta 0.05 --sample-window 20 --target $TARGET 
    run_base client2 stress --tickrate 20 --delta 0.05 --sample-window 20 --target $TARGET 
    run_base client3 stress --tickrate 20 --delta 0.05 --sample-window 20 --target $TARGET 
    run_base client4 stress --tickrate 20 --delta 0.05 --sample-window 20 --target $TARGET 
    run_base client5 stress --tickrate 20 --delta 0.05 --sample-window 20 --target $TARGET 
    run_base client6 stress --tickrate 20 --delta 0.05 --sample-window 20 --target $TARGET 
    run_base client7 stress --tickrate 20 --delta 0.05 --sample-window 20 --target $TARGET 
    run_base client8 stress --tickrate 20 --delta 0.05 --sample-window 20 --target $TARGET 
    run_base client9 stress --tickrate 20 --delta 0.05 --sample-window 20 --target $TARGET 
    run_base client10 stress --tickrate 20 --delta 0.05 --sample-window 20 --target $TARGET 
    run_base client11 stress --tickrate 20 --delta 0.05 --sample-window 20 --target $TARGET 
    run_base client12 stress --tickrate 20 --delta 0.05 --sample-window 20 --target $TARGET 
    run_base client13 stress --tickrate 20 --delta 0.05 --sample-window 20 --target $TARGET 
    run_base client14 stress --tickrate 20 --delta 0.05 --sample-window 20 --target $TARGET 
    run_base client15 stress --tickrate 20 --delta 0.05 --sample-window 20 --target $TARGET 
    run_base client16 stress --tickrate 20 --delta 0.05 --sample-window 20 --target $TARGET 

    wait -n $server
}

@test "stress test tickrate" {
    prepare_log

    run_base server stress --tickrate 2000 --delta 0.05 --sample-window 20 --checksum --server --duration 120 --target $SERVER_POINT
    server=$!
    sleep 1

    run_base client stress --tickrate 2000 --delta 0.05 --sample-window 20 --checksum --target $TARGET 
    client=$!

    wait -n $server
    wait -n $client
}

@test "stress test sample window" {
    prepare_log

    run_base server stress --tickrate 40 --delta 0.05 --sample-window 2000 --checksum --server --duration 120 --target $SERVER_POINT
    server=$!
    sleep 1

    run_base client stress --tickrate 40 --delta 0.05 --sample-window 2000 --checksum --target $TARGET 
    client=$!

    wait -n $server
    wait -n $client
}

@test "stress test delta" {
    prepare_log

    run_base server stress --tickrate 20 --delta 5 --sample-window 20 --checksum --server --duration 120 --target $SERVER_POINT
    server=$!
    sleep 1

    run_base client stress --tickrate 20 --delta 5 --sample-window 20 --checksum --target $TARGET 
    client=$!

    wait -n $server
    wait -n $client
}
